using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.DTO;
using delosfera_server.Modules.Correspondence.Models;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Obligations.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Analytics.Services;

public interface ICalendarService
{
    /// <summary>Датированные события пользователя для календаря сроков (ЗС-13, РС-5).</summary>
    Task<List<CalendarEventDto>> GetAsync(int userId);
}

/// <summary>
/// Календарь сроков (ЗС-13). Основа — датированные задачи из единого реестра
/// (TaskInboxService, все контуры + замещение). Сверх задач (РС-5) добавляются
/// заседания органов, в которых пользователь состоит: это не «моя задача», но дата,
/// которую держат в том же календаре.
/// </summary>
public class CalendarService : ICalendarService
{
    private readonly ITaskInboxService _inbox;
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public CalendarService(ITaskInboxService inbox, DelosferaDbContext db, IBankClock clock)
    {
        _inbox = inbox;
        _db = db;
        _clock = clock;
    }

    public async Task<List<CalendarEventDto>> GetAsync(int userId)
    {
        var inbox = await _inbox.GetAsync(userId);

        var events = inbox.Tasks
            .Where(t => t.DueAt is not null)
            .Select(t => new CalendarEventDto
            {
                Date = DateOnly.FromDateTime(t.DueAt!.Value),
                DueAt = t.DueAt.Value,
                EntityId = t.EntityId,
                DocumentType = t.DocumentType,
                DocumentTypeTitle = t.DocumentTypeTitle,
                RegNumber = t.RegNumber,
                Title = t.DocumentTitle,
                TaskType = t.TaskType,
                IsOverdue = t.IsOverdue,
            })
            .ToList();

        var today = _clock.Today;
        var since = today.AddDays(-45);

        // Письма с моим сроком исполнения (КЛ-1): книга регистрации не заводит поручений
        // в общем реестре задач, поэтому срок ответа — в том числе по запросам НБКР —
        // иначе в календарь не попадёт.
        var letters = await _db.CorrespondenceLetters
            .Where(l => l.DueDate != null
                        && l.ResponsibleUserId == userId
                        && l.Status != LetterStatus.Draft
                        && l.Status != LetterStatus.Answered
                        && l.Status != LetterStatus.Closed
                        && l.Status != LetterStatus.Sent)
            .Select(l => new {l.Id, l.RegNumber, l.Subject, DueDate = l.DueDate!.Value})
            .ToListAsync();

        events.AddRange(letters.Select(l => new CalendarEventDto
        {
            Date = l.DueDate,
            // Срок письма — на конец дня: событие ложится в свой день, а не в предыдущий.
            DueAt = l.DueDate.ToDateTime(new TimeOnly(18, 0)),
            EntityId = l.Id,
            DocumentType = "Letter",
            DocumentTypeTitle = "Письмо",
            RegNumber = l.RegNumber,
            Title = l.Subject,
            TaskType = "Ответ на письмо",
            IsOverdue = l.DueDate < today,
        }));

        // Регулярные обязательства, где пользователь ответственный (КЛ-1): ближайший
        // незакрытый период. Прошедшие периоды не тянем — их закрывают заседания.
        var obligations = await _db.RecurringObligations
            .Where(o => o.IsActive && o.ResponsibleUserId == userId)
            .Select(o => new
            {
                o.Id,
                o.Title,
                Due = o.Periods
                    .Where(p => p.Status == ObligationPeriodStatus.Pending && p.PeriodEnd >= since)
                    .OrderBy(p => p.PeriodStart)
                    .Select(p => (DateOnly?)p.DueDate)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        events.AddRange(obligations.Where(o => o.Due != null).Select(o => new CalendarEventDto
        {
            Date = o.Due!.Value,
            DueAt = o.Due.Value.ToDateTime(new TimeOnly(18, 0)),
            EntityId = o.Id,
            DocumentType = "Obligation",
            DocumentTypeTitle = "Обязательство",
            RegNumber = null,
            Title = o.Title,
            TaskType = "Регулярное обязательство",
            IsOverdue = o.Due.Value < today,
        }));

        // Заседания органов пользователя (РС-5): показываем недавние и будущие, чтобы
        // сетка месяца не тянула всю историю.
        var bodies = await _db.Set<BodyMember>()
            .Where(m => m.UserId == userId)
            .Select(m => m.Body)
            .Distinct()
            .ToListAsync();

        if (bodies.Count > 0)
        {
            var meetings = await _db.Meetings
                .Where(m => bodies.Contains(m.Body) && m.Date >= since)
                .Select(m => new {m.Id, m.Body, m.Number, m.Date})
                .ToListAsync();

            events.AddRange(meetings.Select(m => new CalendarEventDto
            {
                Date = m.Date,
                // Заседания обычно днём; ставим 10:00, чтобы событие легло в свой день.
                DueAt = m.Date.ToDateTime(new TimeOnly(10, 0)),
                EntityId = m.Id,
                DocumentType = "Meeting",
                DocumentTypeTitle = "Заседание",
                RegNumber = $"№{m.Number}",
                Title = $"Заседание — {BodyTitle(m.Body)}",
                TaskType = "Заседание",
                IsOverdue = false,
            }));
        }

        return events.OrderBy(e => e.DueAt).ToList();
    }

    private static string BodyTitle(MeetingBody b) => b switch
    {
        MeetingBody.Board => "Правление",
        MeetingBody.Kpa => "КПА",
        MeetingBody.CreditCommittee => "Кредитный комитет",
        _ => b.ToString(),
    };
}
