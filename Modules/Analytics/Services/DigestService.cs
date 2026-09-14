using delosfera_server.Modules.Analytics.DTO;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Analytics.Services;

public interface IDigestService
{
    /// <summary>Персональный дайджест по всем контурам (УВ-14).</summary>
    Task<DigestDto> GetAsync(int userId);
}

/// <summary>
/// Дайджест (УВ-14): короткая выжимка из единого реестра задач. Ничего не запрашивает
/// сам — переиспользует TaskInboxService, который уже собирает задачи по всем контурам
/// с учётом замещения; дайджест лишь сворачивает их в счётчики и два коротких списка.
/// </summary>
public class DigestService : IDigestService
{
    private readonly ITaskInboxService _inbox;

    public DigestService(ITaskInboxService inbox)
    {
        _inbox = inbox;
    }

    public async Task<DigestDto> GetAsync(int userId)
    {
        var inbox = await _inbox.GetAsync(userId);

        var now = DateTime.UtcNow;
        var soon = now.AddHours(48);

        var contours = inbox.Tasks
            .GroupBy(t => (t.DocumentType, t.DocumentTypeTitle))
            .Select(g => new DigestContourDto
            {
                Contour = g.Key.DocumentType,
                Title = g.Key.DocumentTypeTitle,
                Count = g.Count(),
                Overdue = g.Count(x => x.IsOverdue),
            })
            .OrderByDescending(c => c.Overdue)
            .ThenByDescending(c => c.Count)
            .ToList();

        return new DigestDto
        {
            Total = inbox.Total,
            Overdue = inbox.Overdue,
            DueSoon = inbox.Tasks.Count(t => !t.IsOverdue && t.DueAt is { } d && d <= soon),
            Delegated = inbox.Delegated,
            Contours = contours,
            // Что горит и что на подходе — по шесть строк, чтобы дайджест оставался
            // выжимкой, а не вторым реестром.
            Upcoming = inbox.Tasks
                .Where(t => !t.IsOverdue && t.DueAt != null)
                .OrderBy(t => t.DueAt)
                .Take(6)
                .ToList(),
            OverdueItems = inbox.Tasks
                .Where(t => t.IsOverdue)
                .OrderBy(t => t.DueAt)
                .Take(6)
                .ToList(),
        };
    }
}
