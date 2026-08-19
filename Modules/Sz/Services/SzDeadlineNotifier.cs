using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Sz.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzDeadlineNotifier
{
    /// <summary>Напоминания по срокам исполнения поручений на указанную дату (SZ-03).</summary>
    Task<int> SendAsync(DateOnly today, CancellationToken ct = default);
}

/// <summary>
/// Напоминания о сроках исполнения служебных записок (SZ-03, GEN-12).
///
/// Адресаты по ТЗ — исполнитель, его руководитель и автор записки: срыв срока касается
/// не только того, кто исполняет.
///
/// Повтор в течение дня отсекается по уже отправленным уведомлениям, а не отдельным
/// полем в поручении: система под нагрузкой перезапускается, и признак «напомнили»
/// должен переживать рестарт, не утяжеляя карточку служебными атрибутами.
/// </summary>
public class SzDeadlineNotifier : ISzDeadlineNotifier
{
    private const string Entity = nameof(SzAssignment);

    private readonly DelosferaDbContext _db;
    private readonly INotificationService _notifications;
    private readonly int _remindBeforeDays;
    private readonly ILogger<SzDeadlineNotifier> _logger;

    public SzDeadlineNotifier(
        DelosferaDbContext db,
        INotificationService notifications,
        IConfiguration configuration,
        ILogger<SzDeadlineNotifier> logger)
    {
        _db = db;
        _notifications = notifications;
        // Срок предупреждения настраивается: у разных подразделений разный запас
        // времени на исполнение (SZ-03 «срок настраивается»).
        _remindBeforeDays = configuration.GetValue("Notifications:SzRemindBeforeDays", 3);
        _logger = logger;
    }

    public async Task<int> SendAsync(DateOnly today, CancellationToken ct = default)
    {
        // Сданное на приёмку поручение из напоминаний уходит: исполнитель свою часть
        // сделал, и торопить его нечем — дальше очередь принимающего.
        var assignments = await _db.SzAssignments
            .Include(a => a.AssigneeUser)
            .Include(a => a.AssigneeUnit)
            .Include(a => a.SzDocument!).ThenInclude(d => d.Document)
            .Where(a => a.DueDate != null && a.State == SzAssignmentState.Open)
            .ToListAsync(ct);

        if (assignments.Count == 0) return 0;

        var ids = assignments.Select(a => a.Id).ToList();

        // Что уже отправлено сегодня по этим поручениям.
        var since = DateTime.UtcNow.AddHours(-20);
        var alreadyNotified = await _db.Notifications
            .Where(n => n.EntityType == Entity && n.EntityId != null
                        && ids.Contains(n.EntityId.Value) && n.CreatedAt >= since)
            .Select(n => n.EntityId!.Value)
            .ToListAsync(ct);

        var sent = 0;

        foreach (var assignment in assignments)
        {
            if (alreadyNotified.Contains(assignment.Id)) continue;

            var due = assignment.DueDate!.Value;
            var daysLeft = due.DayNumber - today.DayNumber;

            string title, body;
            NotificationSeverity severity;

            if (daysLeft == _remindBeforeDays)
            {
                title = "Приближается срок исполнения поручения";
                body = $"Срок исполнения поручения по служебной записке «{Describe(assignment)}» " +
                       $"истекает через {_remindBeforeDays} дн. — {due:dd.MM.yyyy}.";
                severity = NotificationSeverity.Warning;
            }
            else if (daysLeft == 0)
            {
                title = "Сегодня истекает срок исполнения поручения";
                body = $"Срок исполнения поручения по служебной записке «{Describe(assignment)}» " +
                       "истекает сегодня.";
                severity = NotificationSeverity.Warning;
            }
            else if (daysLeft < 0)
            {
                title = "Поручение просрочено";
                body = $"Срок исполнения поручения по служебной записке «{Describe(assignment)}» " +
                       $"истёк {due:dd.MM.yyyy} — просрочка {-daysLeft} дн.";
                severity = NotificationSeverity.Urgent;
            }
            else
            {
                continue;
            }

            var recipients = await RecipientsAsync(assignment, ct);
            if (recipients.Count == 0) continue;

            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = title,
                BodyRu = body + $" Текст поручения: {assignment.Text}",
                Category = NotificationCategory.Task,
                Severity = severity,
                EntityType = Entity,
                EntityId = assignment.Id,
                Url = $"/sz/{assignment.SzDocumentId}",
                UserIds = recipients,
            }, null);

            sent++;
        }

        if (sent > 0)
            _logger.LogInformation("Напоминаний по срокам поручений СЗ разослано: {Count}", sent);

        return sent;
    }

    private async Task<List<int>> RecipientsAsync(SzAssignment assignment, CancellationToken ct)
    {
        var recipients = new HashSet<int> {assignment.AssigneeUserId, assignment.CreatedByUserId};

        // Руководитель подразделения исполнителя — по ТЗ он тоже в круге адресатов.
        if (assignment.AssigneeUnitId is { } unitId)
        {
            var head = await _db.OrganizationUnits
                .Where(u => u.Id == unitId).Select(u => u.HeadUserId).FirstOrDefaultAsync(ct);

            if (head is { } headId) recipients.Add(headId);
        }

        // Автор записки — в карточке документа: контурная запись хранит только
        // реквизиты исполнения.
        var author = assignment.SzDocument?.Document?.AuthorId;
        if (author is { } authorId) recipients.Add(authorId);

        return recipients.ToList();
    }

    private static string Describe(SzAssignment assignment)
    {
        var document = assignment.SzDocument?.Document;

        return !string.IsNullOrWhiteSpace(document?.RegNumber)
            ? document!.RegNumber!
            : document?.Title ?? $"№{assignment.SzDocumentId}";
    }
}
