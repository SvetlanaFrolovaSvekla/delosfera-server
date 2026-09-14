using delosfera_server.Modules.Analytics.DTO;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Analytics.Services;

public interface ICalendarService
{
    /// <summary>Датированные задачи пользователя для календаря сроков (ЗС-13).</summary>
    Task<List<CalendarEventDto>> GetAsync(int userId);
}

/// <summary>
/// Календарь сроков (ЗС-13). Как и дайджест, ничего не запрашивает сам — берёт задачи
/// из единого реестра (TaskInboxService, все контуры + замещение) и оставляет те, у
/// которых есть срок. Раскладку по дням делает клиент на сетке месяца.
/// </summary>
public class CalendarService : ICalendarService
{
    private readonly ITaskInboxService _inbox;

    public CalendarService(ITaskInboxService inbox)
    {
        _inbox = inbox;
    }

    public async Task<List<CalendarEventDto>> GetAsync(int userId)
    {
        var inbox = await _inbox.GetAsync(userId);

        return inbox.Tasks
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
            .OrderBy(e => e.DueAt)
            .ToList();
    }
}
