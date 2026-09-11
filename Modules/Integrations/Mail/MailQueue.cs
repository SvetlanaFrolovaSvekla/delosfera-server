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
        MailAttachment? attachment = null, CancellationToken ct = default);

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
    private readonly IMailSettingsService _settings;
    private readonly delosfera_server.Common.Security.ISecretProtector _protector;
    private readonly ILogger<MailQueue> _logger;

    public MailQueue(
        DelosferaDbContext db,
        IMailSettingsService settings,
        delosfera_server.Common.Security.ISecretProtector protector,
        ILogger<MailQueue> logger)
    {
        _db = db;
        _settings = settings;
        _protector = protector;
        _logger = logger;
    }

    /// <summary>
    /// Читаем настройки при каждом обращении, а не запоминаем при создании:
    /// администратор выключает рассылку в интерфейсе и вправе ожидать, что она
    /// прекратится сразу, а не после перезапуска приложения.
    /// </summary>
    public bool Enabled => _settings.LoadAsync().GetAwaiter().GetResult().Enabled;

    public async Task EnqueueAsync(
        IEnumerable<int> userIds, string subject, string body, string? url, int? notificationId,
        MailAttachment? attachment = null, CancellationToken ct = default)
    {
        var settings = await _settings.LoadAsync(ct);
        if (!settings.Enabled) return;

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
                Body = BuildBody(recipient.FullName, body, url, settings.BaseUrl),
                NotificationId = notificationId,
                CreatedAt = DateTime.UtcNow,
                AttachmentBytes = attachment?.Bytes,
                AttachmentFileName = attachment?.FileName,
                AttachmentContentType = attachment?.ContentType,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> FlushAsync(CancellationToken ct = default)
    {
        var settings = await _settings.LoadAsync(ct);
        if (!settings.Enabled) return 0;

        var pending = await _db.OutgoingEmails
            .Where(e => e.SentAt == null && !e.Failed)
            .OrderBy(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        using var client = CreateClient(settings);
        var sent = 0;

        foreach (var email in pending)
        {
            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(settings.FromAddress, settings.FromName),
                    Subject = email.Subject,
                    Body = email.Body,
                    IsBodyHtml = false,
                };
                message.To.Add(email.ToAddress);

                // Вложение живёт только на время отправки: MemoryStream закрывается вместе с
                // Attachment (Dispose пробрасывается), а исходные байты остаются в email —
                // при ошибке отправки повторная попытка вложение не потеряет.
                using var attachmentStream = email.AttachmentBytes is { } bytes
                    ? new MemoryStream(bytes)
                    : null;

                if (attachmentStream is not null)
                {
                    message.Attachments.Add(new Attachment(
                        attachmentStream,
                        email.AttachmentFileName ?? "attachment",
                        email.AttachmentContentType ?? "application/octet-stream"));
                }

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
                if (email.Attempts >= settings.MaxAttempts)
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

    private SmtpClient CreateClient(MailSettings settings)
    {
        var client = new SmtpClient(settings.Host, settings.Port) {EnableSsl = settings.UseSsl};

        if (!string.IsNullOrWhiteSpace(settings.User))
        {
            client.Credentials = new NetworkCredential(
                settings.User,
                string.IsNullOrEmpty(settings.PasswordEncrypted)
                    ? string.Empty
                    : _protector.Unprotect(settings.PasswordEncrypted));
        }
        else
        {
            // Внутренний relay пускает по адресу сервера. Явно снимаем учётные данные
            // Windows-сессии, которые SmtpClient иначе подставляет сам.
            client.UseDefaultCredentials = false;
        }

        return client;
    }

    private static string BuildBody(string fullName, string body, string? url, string baseUrl)
    {
        var link = string.IsNullOrWhiteSpace(url)
            ? string.Empty
            : $"\n\nОткрыть в системе: {baseUrl.TrimEnd('/')}{url}";

        return $"""
                {fullName}, здравствуйте!

                {body}{link}

                --
                Письмо сформировано системой электронного документооборота автоматически.
                Отвечать на него не нужно.
                """;
    }
}
