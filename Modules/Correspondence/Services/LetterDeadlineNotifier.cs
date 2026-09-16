using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Correspondence.Models;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;

namespace delosfera_server.Modules.Correspondence.Services;

public interface ILetterDeadlineNotifier
{
    /// <summary>Напоминания по срокам исполнения писем на указанную дату (КР-1).</summary>
    Task<int> SendAsync(DateOnly today, CancellationToken ct = default);
}

/// <summary>
/// Напоминания о сроках исполнения корреспонденции (КР-1).
///
/// Письма регулятора и обращения клиентов приходят со сроком ответа, а карточка
/// письма — в отличие от служебной записки — не порождает поручений в общем реестре
/// задач: без отдельного напоминания срок виден только тому, кто сам открыл книгу.
/// Для запросов НБКР это прямой комплаенс-риск.
///
/// Повтор в течение дня отсекается по уже отправленным уведомлениям, а не полем в
/// письме: признак «напомнили» должен переживать рестарт, как и в напоминаниях по СЗ.
/// </summary>
public class LetterDeadlineNotifier : ILetterDeadlineNotifier
{
    private const string Entity = nameof(CorrespondenceLetter);

    private readonly DelosferaDbContext _db;
    private readonly INotificationService _notifications;
    private readonly int _remindBeforeDays;
    private readonly ILogger<LetterDeadlineNotifier> _logger;

    public LetterDeadlineNotifier(
        DelosferaDbContext db,
        INotificationService notifications,
        IConfiguration configuration,
        ILogger<LetterDeadlineNotifier> logger)
    {
        _db = db;
        _notifications = notifications;
        _remindBeforeDays = configuration.GetValue("Notifications:CorrespondenceRemindBeforeDays", 3);
        _logger = logger;
    }

    public async Task<int> SendAsync(DateOnly today, CancellationToken ct = default)
    {
        // Черновики, отвеченные, закрытые и отправленные исходящие срок уже не держат.
        var letters = await _db.CorrespondenceLetters
            .Where(l => l.DueDate != null
                        && l.Status != LetterStatus.Draft
                        && l.Status != LetterStatus.Answered
                        && l.Status != LetterStatus.Closed
                        && l.Status != LetterStatus.Sent)
            .ToListAsync(ct);

        if (letters.Count == 0) return 0;

        var ids = letters.Select(l => l.Id).ToList();

        // Что уже отправлено сегодня по этим письмам.
        var since = DateTime.UtcNow.AddHours(-20);
        var alreadyNotified = await _db.Notifications
            .Where(n => n.EntityType == Entity && n.EntityId != null
                        && ids.Contains(n.EntityId.Value) && n.CreatedAt >= since)
            .Select(n => n.EntityId!.Value)
            .ToListAsync(ct);

        var sent = 0;

        foreach (var letter in letters)
        {
            if (alreadyNotified.Contains(letter.Id)) continue;

            var due = letter.DueDate!.Value;
            var daysLeft = due.DayNumber - today.DayNumber;

            string title, body;
            NotificationSeverity severity;

            if (daysLeft == _remindBeforeDays)
            {
                title = "Приближается срок ответа на письмо";
                body = $"Срок исполнения по письму «{Describe(letter)}» истекает через " +
                       $"{_remindBeforeDays} дн. — {due:dd.MM.yyyy}.";
                severity = NotificationSeverity.Warning;
            }
            else if (daysLeft == 0)
            {
                title = "Сегодня истекает срок ответа на письмо";
                body = $"Срок исполнения по письму «{Describe(letter)}» истекает сегодня.";
                severity = NotificationSeverity.Warning;
            }
            else if (daysLeft < 0)
            {
                title = "Письмо просрочено";
                body = $"Срок исполнения по письму «{Describe(letter)}» истёк {due:dd.MM.yyyy} — " +
                       $"просрочка {-daysLeft} дн.";
                severity = NotificationSeverity.Urgent;
            }
            else
            {
                continue;
            }

            var recipients = await RecipientsAsync(letter, ct);
            if (recipients.Count == 0) continue;

            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = title,
                BodyRu = body,
                Category = NotificationCategory.Other,
                Severity = severity,
                EntityType = Entity,
                EntityId = letter.Id,
                Url = "/correspondence",
                UserIds = recipients,
            }, null);

            sent++;
        }

        if (sent > 0)
            _logger.LogInformation("Напоминаний по срокам корреспонденции разослано: {Count}", sent);

        return sent;
    }

    private async Task<List<int>> RecipientsAsync(CorrespondenceLetter letter, CancellationToken ct)
    {
        var recipients = new HashSet<int>();

        // Исполнитель — первый адресат; если не назначен, письмо ведёт тот, кто его
        // зарегистрировал, и напоминание не должно потеряться.
        if (letter.ResponsibleUserId is { } responsible) recipients.Add(responsible);
        recipients.Add(letter.CreatedByUserId);

        // Руководитель подразделения-исполнителя — как и в напоминаниях по СЗ.
        if (letter.ResponsibleUnitId is { } unitId)
        {
            var head = await _db.OrganizationUnits
                .Where(u => u.Id == unitId).Select(u => u.HeadUserId).FirstOrDefaultAsync(ct);

            if (head is { } headId) recipients.Add(headId);
        }

        return recipients.ToList();
    }

    private static string Describe(CorrespondenceLetter letter) =>
        !string.IsNullOrWhiteSpace(letter.RegNumber) ? letter.RegNumber! : letter.Subject;
}
