using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using delosfera_server.Data;

namespace delosfera_server.Modules.Integrations.Mail;

public interface IMailQueue
{
    bool Enabled { get; }

    /// <summary>Поставить уведомление в очередь на отправку получателям.</summary>
    Task EnqueueAsync(
        IEnumerable<int> userIds, string subject, string body, string? url, int? notificationId,
        CancellationToken ct = default);

    /// <summary>Отправить накопившееся. Возвращает число ушедших писем.</summary>
    Task<int> FlushAsync(CancellationToken ct = default);
}

/// <summary>
/// Очередь исходящих писем (INT-02).
///
/// Уведомление кладётся в очередь в той же транзакции, что и запись в системе, а
/// отправляется отдельно: relay банка отвечает медленно и иногда лежит, и согласование
/// не должно ждать почтовый сервер.
/// </summary>
public class MailQueue : IMailQueue
{
    private readonly DelosferaDbContext _db;
    private readonly MailOptions _options;
    private readonly ILogger<MailQueue> _logger;

    public MailQueue(DelosferaDbContext db, IOptions<MailOptions> options, ILogger<MailQueue> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public bool Enabled => _options.Enabled;

    public async Task EnqueueAsync(
        IEnumerable<int> userIds, string subject, string body, string? url, int? notificationId,
        CancellationToken ct = default)
    {
        if (!_options.Enabled) return;

        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        // Заблокированным и деактивированным не пишем: уведомление о задаче человеку,
        // которому закрыт доступ, — лишний повод для вопросов службе безопасности.
        var recipients = await _db.Users
            .Where(u => ids.Contains(u.Id) && u.IsActive && u.BlockedAt == null && u.Email != "")
            .Select(u => new {u.Email, u.FullName})
            .ToListAsync(ct);

        foreach (var recipient in recipients)
        {
            _db.OutgoingEmails.Add(new OutgoingEmail
            {
                ToAddress = recipient.Email,
                Subject = subject,
                Body = BuildBody(recipient.FullName, body, url),
                NotificationId = notificationId,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> FlushAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled) return 0;

        var pending = await _db.OutgoingEmails
            .Where(e => e.SentAt == null && !e.Failed)
            .OrderBy(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        using var client = CreateClient();
        var sent = 0;

        foreach (var email in pending)
        {
            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_options.FromAddress, _options.FromName),
                    Subject = email.Subject,
                    Body = email.Body,
                    IsBodyHtml = false,
                };
                message.To.Add(email.ToAddress);

                await client.SendMailAsync(message, ct);

                email.SentAt = DateTime.UtcNow;
                email.LastError = null;
                sent++;
            }
            catch (Exception ex)
            {
                email.Attempts++;
                email.LastError = ex.Message;

                // Исчерпав попытки, письмо помечается неудачей, а не удаляется: иначе
                // недоставленное уведомление исчезнет вместе со следом о проблеме.
                if (email.Attempts >= _options.MaxAttempts)
                {
                    email.Failed = true;
                    _logger.LogError(ex,
                        "Письмо на {Address} не отправлено после {Attempts} попыток",
                        email.ToAddress, email.Attempts);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return sent;
    }

    private SmtpClient CreateClient()
    {
        var client = new SmtpClient(_options.Host, _options.Port) {EnableSsl = _options.UseSsl};

        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            client.Credentials = new NetworkCredential(_options.User, _options.Password);
        }
        else
        {
            // Внутренний relay пускает по адресу сервера. Явно снимаем учётные данные
            // Windows-сессии, которые SmtpClient иначе подставляет сам.
            client.UseDefaultCredentials = false;
        }

        return client;
    }

    private string BuildBody(string fullName, string body, string? url)
    {
        var link = string.IsNullOrWhiteSpace(url)
            ? string.Empty
            : $"\n\nОткрыть в системе: {_options.BaseUrl.TrimEnd('/')}{url}";

        return $"""
                {fullName}, здравствуйте!

                {body}{link}

                --
                Письмо сформировано системой электронного документооборота автоматически.
                Отвечать на него не нужно.
                """;
    }
}
