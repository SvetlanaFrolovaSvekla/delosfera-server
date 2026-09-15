using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.DTO;
using delosfera_server.Modules.Correspondence.Models;
using delosfera_server.Modules.Obligations.Models;
using delosfera_server.Modules.Workflow.DTO;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Analytics.Services;

public interface IDigestService
{
    /// <summary>Персональный дайджест по всем контурам (УВ-14).</summary>
    Task<DigestDto> GetAsync(int userId);
}

/// <summary>
/// Дайджест (УВ-14): короткая выжимка сроков по всем контурам. Основа — единый реестр
/// задач (TaskInboxService, все контуры + замещение). Сверх него (КЛ-1) добавляются
/// сроки, которые в реестр задач не попадают: ответы на письма и регулярные
/// обязательства — их держат по дате, а не по поручению.
/// </summary>
public class DigestService : IDigestService
{
    private readonly ITaskInboxService _inbox;
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public DigestService(ITaskInboxService inbox, DelosferaDbContext db, IBankClock clock)
    {
        _inbox = inbox;
        _db = db;
        _clock = clock;
    }

    public async Task<DigestDto> GetAsync(int userId)
    {
        var inbox = await _inbox.GetAsync(userId);

        var now = DateTime.UtcNow;
        var soon = now.AddHours(48);
        var today = _clock.Today;

        // Единый список: задачи реестра плюс сроки писем и обязательств, чтобы счётчики
        // и списки дайджеста считались по всем контурам одинаково.
        var tasks = new List<InboxTaskDto>(inbox.Tasks);
        tasks.AddRange(await LetterTasksAsync(userId, today));
        tasks.AddRange(await ObligationTasksAsync(userId, today));

        var contours = tasks
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
            Total = tasks.Count,
            Overdue = tasks.Count(t => t.IsOverdue),
            DueSoon = tasks.Count(t => !t.IsOverdue && t.DueAt is { } d && d <= soon),
            Delegated = inbox.Delegated,
            Contours = contours,
            // Что горит и что на подходе — по шесть строк, чтобы дайджест оставался
            // выжимкой, а не вторым реестром.
            Upcoming = tasks
                .Where(t => !t.IsOverdue && t.DueAt != null)
                .OrderBy(t => t.DueAt)
                .Take(6)
                .ToList(),
            OverdueItems = tasks
                .Where(t => t.IsOverdue)
                .OrderBy(t => t.DueAt)
                .Take(6)
                .ToList(),
        };
    }

    /// <summary>Письма с моим сроком исполнения как строки реестра задач (КЛ-1).</summary>
    private async Task<List<InboxTaskDto>> LetterTasksAsync(int userId, DateOnly today)
    {
        var letters = await _db.CorrespondenceLetters
            .Where(l => l.DueDate != null
                        && l.ResponsibleUserId == userId
                        && l.Status != LetterStatus.Draft
                        && l.Status != LetterStatus.Answered
                        && l.Status != LetterStatus.Closed
                        && l.Status != LetterStatus.Sent)
            .Select(l => new {l.Id, l.RegNumber, l.Subject, DueDate = l.DueDate!.Value, l.CreatedAt})
            .ToListAsync();

        return letters.Select(l => new InboxTaskDto
        {
            TaskId = l.Id,
            DocumentId = l.Id,
            EntityId = l.Id,
            RegNumber = l.RegNumber,
            DocumentTitle = l.Subject,
            DocumentType = "Letter",
            DocumentTypeTitle = "Письмо",
            TaskType = "Ответ на письмо",
            DueAt = l.DueDate.ToDateTime(new TimeOnly(18, 0)),
            IsOverdue = l.DueDate < today,
            CreatedAt = l.CreatedAt,
        }).ToList();
    }

    /// <summary>Регулярные обязательства, где я ответственный, как строки реестра (КЛ-1).</summary>
    private async Task<List<InboxTaskDto>> ObligationTasksAsync(int userId, DateOnly today)
    {
        var since = today.AddDays(-45);

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

        return obligations.Where(o => o.Due != null).Select(o => new InboxTaskDto
        {
            TaskId = o.Id,
            DocumentId = o.Id,
            EntityId = o.Id,
            RegNumber = null,
            DocumentTitle = o.Title,
            DocumentType = "Obligation",
            DocumentTypeTitle = "Обязательство",
            TaskType = "Регулярное обязательство",
            DueAt = o.Due!.Value.ToDateTime(new TimeOnly(18, 0)),
            IsOverdue = o.Due.Value < today,
            CreatedAt = DateTime.UtcNow,
        }).ToList();
    }
}
