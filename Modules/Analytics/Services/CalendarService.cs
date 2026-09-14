using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.DTO;
using delosfera_server.Modules.Meetings.Models;
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

        // Заседания органов пользователя (РС-5): показываем недавние и будущие, чтобы
        // сетка месяца не тянула всю историю.
        var bodies = await _db.Set<BodyMember>()
            .Where(m => m.UserId == userId)
            .Select(m => m.Body)
            .Distinct()
            .ToListAsync();

        if (bodies.Count > 0)
        {
            var since = _clock.Today.AddDays(-45);
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
