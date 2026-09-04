using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;

namespace delosfera_server.Modules.Meetings.Services;

public interface IMeetingNotificationService
{
    /// <summary>Кнопка «Отправить уведомление» — рассылка о предстоящем заседании.</summary>
    Task<MeetingNotifyResultDto> NotifyAboutMeetingAsync(int meetingId, int currentUserId);

    /// <summary>Напоминания об исполнении протоколов на указанную дату. Возвращает число разосланных.</summary>
    Task<int> SendDueRemindersAsync(DateOnly today, CancellationToken ct = default);
}

/// <summary>
/// Уведомления по заседаниям.
///
/// Рассылка идёт через общий сервис уведомлений системы: письмо на почту отправляет он же,
/// а здесь собираются адресаты и текст по формулировкам ТЗ. Тексты держатся в одном месте,
/// потому что напоминания за 5 дней, в день срока и после просрочки различаются одной фразой —
/// разнеси их по вызовам, и формулировки разъедутся при первой же правке.
/// </summary>
public class MeetingNotificationService : IMeetingNotificationService
{
    private readonly DelosferaDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<MeetingNotificationService> _logger;

    public MeetingNotificationService(
        DelosferaDbContext db,
        INotificationService notifications,
        ILogger<MeetingNotificationService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>
    /// Повестка строкой: номер вопроса и тема.
    ///
    /// Человеку нужно знать не только дату заседания, но и каким по счёту слушают
    /// его вопрос — иначе он приходит к началу и ждёт всё заседание.
    /// </summary>
    private static string Questions(Meeting meeting)
    {
        if (meeting.Items.Count == 0) return "";

        var lines = meeting.Items
            .OrderBy(i => i.Order)
            .Select(i => $"{i.Order}. {i.Topic}");

        return $"Вопросы повестки: {string.Join("; ", lines)}. ";
    }

    public async Task<MeetingNotifyResultDto> NotifyAboutMeetingAsync(int meetingId, int currentUserId)
    {
        var meeting = await _db.Meetings
            .Include(m => m.Items).ThenInclude(i => i.Guests)
            .Include(m => m.Items).ThenInclude(i => i.SourceSz).ThenInclude(s => s!.Document)
            .FirstOrDefaultAsync(m => m.Id == meetingId)
            ?? throw new KeyNotFoundException("Заседание не найдено");

        var recipients = new HashSet<int> { meeting.SecretaryUserId };

        foreach (var item in meeting.Items)
        {
            if (item.SpeakerUserId is { } speaker) recipients.Add(speaker);
            if (item.SpeakerHeadUserId is { } head) recipients.Add(head);
            if (item.DeputySecretaryUserId is { } deputy) recipients.Add(deputy);
            foreach (var guest in item.Guests) recipients.Add(guest.UserId);

            // Автор записки, из которой вырос вопрос. Он просил вынести вопрос на
            // орган и до сих пор узнавал о заседании последним — или не узнавал:
            // докладчиком по своей записке автор бывает не всегда.
            if (item.SourceSz?.Document?.AuthorId is { } author) recipients.Add(author);
        }

        foreach (var member in await MembersOfAsync(meeting.Body)) recipients.Add(member);

        var subject = $"Уведомление о заседании {MeetingTitles.BodyGenitive(meeting.Body)}";
        var body =
            $"Добрый день, уважаемые коллеги! {meeting.Date:dd.MM.yyyy} в {meeting.Time:HH\\:mm} " +
            $"состоится заседание {MeetingTitles.BodyGenitive(meeting.Body)}. " +
            Questions(meeting) +
            $"Материалы размещены по ссылке{Link(meeting.MaterialsUrl)}";

        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            TitleRu = subject,
            BodyRu = body,
            Category = NotificationCategory.Task,
            Severity = NotificationSeverity.Info,
            EntityType = nameof(Meeting),
            EntityId = meeting.Id,
            Url = $"/meetings/{meeting.Id}",
            UserIds = recipients.ToList(),
        }, currentUserId);

        // Отметка о рассылке — момент, а не календарная дата: в базе это timestamptz.
        meeting.NotifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var names = await _db.Users
            .Where(u => recipients.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => u.FullName)
            .ToListAsync();

        return new MeetingNotifyResultDto
        {
            MeetingId = meeting.Id,
            RecipientCount = recipients.Count,
            Subject = subject,
            Body = body,
            Recipients = names,
        };
    }

    public async Task<int> SendDueRemindersAsync(DateOnly today, CancellationToken ct = default)
    {
        var openStatuses = new[] { ExecutionStatus.New, ExecutionStatus.InProgress };

        var assignments = await _db.AgendaAssignments
            .Include(a => a.AgendaItem).ThenInclude(i => i!.Meeting)
            .Where(a => a.DueDate != null
                        && openStatuses.Contains(a.Status)
                        && (a.LastReminderOn == null || a.LastReminderOn < today))
            .ToListAsync(ct);

        var sent = 0;

        foreach (var assignment in assignments)
        {
            var due = assignment.DueDate!.Value;
            var daysLeft = due.DayNumber - today.DayNumber;

            // Напоминаем ровно в три момента: за пять дней, в день срока и по просрочке.
            // Всё, что между ними, шумит и приучает игнорировать письма.
            var stage = daysLeft switch
            {
                5 => ReminderStage.FiveDays,
                0 => ReminderStage.DueToday,
                < 0 => ReminderStage.Overdue,
                _ => (ReminderStage?)null,
            };

            if (stage is null) continue;

            var item = assignment.AgendaItem!;
            var meeting = item.Meeting!;

            var recipients = new HashSet<int> { assignment.UserId, meeting.SecretaryUserId };
            if (item.SpeakerUserId is { } speaker) recipients.Add(speaker);
            if (item.SpeakerHeadUserId is { } head) recipients.Add(head);
            if (item.DeputySecretaryUserId is { } deputy) recipients.Add(deputy);
            if (item.ControllerUserId is { } controller) recipients.Add(controller);

            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = $"Исполнение Протокола {MeetingTitles.BodyGenitive(meeting.Body)}",
                BodyRu = ReminderBody(stage.Value, meeting, item, due),
                Category = NotificationCategory.Task,
                Severity = stage == ReminderStage.Overdue
                    ? NotificationSeverity.Urgent
                    : NotificationSeverity.Warning,
                EntityType = nameof(AgendaItem),
                EntityId = item.Id,
                Url = $"/meetings/{meeting.Id}",
                UserIds = recipients.ToList(),
            }, meeting.SecretaryUserId);

            assignment.LastReminderOn = today;
            sent++;
        }

        if (sent > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Напоминаний об исполнении протоколов разослано: {Count}", sent);
        }

        return sent;
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private enum ReminderStage { FiveDays, DueToday, Overdue }

    private static string ReminderBody(ReminderStage stage, Meeting meeting, AgendaItem item, DateOnly due)
    {
        var protocol = string.IsNullOrWhiteSpace(item.ProtocolNumber) ? "б/н" : item.ProtocolNumber;
        var protocolDate = (item.ProtocolDate ?? meeting.Date).ToString("dd.MM.yyyy");
        var body = MeetingTitles.BodyGenitive(meeting.Body);

        var head = $"Добрый день! Срок исполнения Протокола {body} от {protocolDate} г. № {protocol} ";

        var middle = stage switch
        {
            ReminderStage.FiveDays => $"истекает через 5 (пять) дней - {due:dd.MM.yyyy} г.",
            ReminderStage.DueToday => "истекает сегодня.",
            _ => $"истек {due:dd.MM.yyyy} г.",
        };

        return head + middle +
               " Необходимо заполнить отчет об исполнении по ссылке " +
               $"[журнал заседаний, заседание № {meeting.Number:D2} от {meeting.Date:dd.MM.yyyy}].";
    }

    private static string Link(string? url) =>
        string.IsNullOrWhiteSpace(url) ? "." : $": {url}";

    /// <summary>
    /// Члены органа определяются правом роли, а не отдельным списком: состав меняется
    /// приказом, и вести его вторым справочником — гарантированно получить расхождение.
    /// </summary>
    private async Task<List<int>> MembersOfAsync(MeetingBody body)
    {
        var code = (int)MeetingAccessService.MembershipFor(body);

        return await _db.Users
            .Where(u => u.IsActive && u.Roles.Any(r => r.PermissionCodes.Contains(code)))
            .Select(u => u.Id)
            .ToListAsync();
    }
}
