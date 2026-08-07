using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public class TasksService : ITasksService
{
    private readonly DelosferaDbContext _db;

    public TasksService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<VndTaskResponse>> GetCoordinationTasksAsync(int userId)
    {
        var stages = await _db.Set<VndApprovalStage>()
            .Include(s => s.ApprovalProcess).ThenInclude(p => p!.Vnd)
            .Include(s => s.ApprovalProcess).ThenInclude(p => p!.Redaction)
            .Where(s => s.ApproverUserId == userId)
            .Where(s =>
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.Primary
                 && s.PrimaryDecision == ApprovalStageDecision.Pending)
                ||
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.Repeated
                 && s.ParticipatesInRepeat
                 && (s.RepeatDecision == null || s.RepeatDecision == ApprovalStageDecision.Pending)))
            .ToListAsync();

        var initiatorIds = stages.Select(s => s.ApprovalProcess!.InitiatorUserId).Distinct().ToList();
        var initiators = await _db.Users
            .Where(u => initiatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return stages.Select(s =>
        {
            var process = s.ApprovalProcess!;
            var isPrimaryPhase = process.Status == ApprovalProcessStatus.Primary;

            return new VndTaskResponse
            {
                VndId = process.VndId,
                VndCode = process.Vnd!.Code,
                VndTitle = process.Vnd!.TitleRu,
                Scope = "coordination",
                RedactionId = process.RedactionId,
                RedactionCode = process.Redaction!.Code,
                StageId = s.Id,
                StagePhase = isPrimaryPhase ? "primary" : "repeat",
                DeadlineAt = isPrimaryPhase ? process.PrimaryDeadlineAt : process.RepeatDeadlineAt,
                InitiatorName = initiators.GetValueOrDefault(process.InitiatorUserId, "—"),
                DeadlineMinutes = isPrimaryPhase ? process.PrimaryDeadlineMinutes : process.RepeatDeadlineMinutes,
                CreatedAt = isPrimaryPhase ? process.PrimaryStartedAt : (process.RepeatStartedAt ?? process.CreatedAt)
            };
        })
        .OrderBy(t => t.DeadlineAt)
        .ToList();
    }

    /// <summary>Видит только инициатор (создатель) ВНД — кураторы, начальники подразделений
    /// и ответственные исполнители в этот список больше не попадают, независимо от связи с документом.</summary>
    public async Task<List<VndTaskResponse>> GetActualizationTasksAsync(int userId)
    {
        var openVndIds = await GetOpenActualizationVndIdsAsync();
        if (openVndIds.Count == 0) return new List<VndTaskResponse>();

        var docs = await _db.VndDocuments
            .Where(x => openVndIds.Contains(x.Id) && x.CreatedByUserId == userId)
            .ToListAsync();

        return docs.Select(x => new VndTaskResponse
            {
                VndId = x.Id,
                VndCode = x.Code,
                VndTitle = x.TitleRu,
                Scope = "actualization",
                DueActualizationDate = x.DueActualizationDate,
                CreatedAt = x.UpdatedAt
            })
            .OrderBy(t => t.DueActualizationDate)
            .ToList();
    }

    /// <summary>Видит только инициатор (создатель) ВНД — та же логика, что и для актуализации.</summary>
    public async Task<List<VndTaskResponse>> GetConsolidationTasksAsync(int userId)
    {
        var docs = await _db.VndDocuments
            .Where(x => x.Status == VndStatus.Consolidation && x.CreatedByUserId == userId)
            .ToListAsync();

        if (docs.Count == 0) return new List<VndTaskResponse>();

        return docs.Select(x => new VndTaskResponse
            {
                VndId = x.Id,
                VndCode = x.Code,
                VndTitle = x.TitleRu,
                Scope = "consolidation",
                StatusLabel = "В процессе консолидации",
                DueActualizationDate = x.DueActualizationDate,
                CreatedAt = x.UpdatedAt
            })
            .OrderBy(t => t.DueActualizationDate)
            .ToList();
    }

    public async Task<List<VndTaskResponse>> GetMyVndApprovalTasksAsync(int userId)
    {
        var vnds = await _db.VndDocuments
            .Where(x => x.Status == VndStatus.Review)
            .ToListAsync();

        if (vnds.Count == 0) return new List<VndTaskResponse>();

        var vndIds = vnds.Select(x => x.Id).ToList();
        var processByVndId = await GetCurrentApprovalProcessesByVndIdAsync(vndIds);
        var openActualizationVndIds = await GetOpenActualizationVndIdsAsync(vndIds);

        var result = new List<VndTaskResponse>();
        foreach (var vnd in vnds)
        {
            if (!processByVndId.TryGetValue(vnd.Id, out var process)) continue;

            var isRelevant = process.InitiatorUserId == userId || vnd.ActualizationResponsibleUserId == userId;
            if (!isRelevant) continue;

            var statusLabel = openActualizationVndIds.Contains(vnd.Id)
                ? "В процессе согласования по актуализации ВНД"
                : "В процессе согласования первой редакции ВНД";

            result.Add(new VndTaskResponse
            {
                VndId = vnd.Id,
                VndCode = vnd.Code,
                VndTitle = vnd.TitleRu,
                Scope = "myVndApproval",
                RedactionId = process.RedactionId,
                RedactionCode = process.Redaction?.Code,
                StatusLabel = statusLabel,
                CreatedAt = vnd.UpdatedAt
            });
        }

        return result.OrderBy(t => t.CreatedAt).ToList();
    }

    private async Task<HashSet<int>> GetOpenActualizationVndIdsAsync(List<int>? restrictToVndIds = null)
    {
        var query = _db.Set<VndActualizationRecord>().Where(r => r.PublishedAt == null);
        if (restrictToVndIds is not null)
            query = query.Where(r => restrictToVndIds.Contains(r.VndId));

        var ids = await query.Select(r => r.VndId).Distinct().ToListAsync();
        return ids.ToHashSet();
    }

    private async Task<Dictionary<int, VndApprovalProcess>> GetCurrentApprovalProcessesByVndIdAsync(
        List<int> vndIds)
    {
        var processes = await _db.VndApprovalProcesses
            .Include(p => p.Redaction)
            .Where(p => vndIds.Contains(p.VndId)
                        && p.Status != ApprovalProcessStatus.Approved
                        && p.Status != ApprovalProcessStatus.Cancelled
                        && p.Status != ApprovalProcessStatus.Rejected)
            .ToListAsync();

        return processes
            .GroupBy(p => p.VndId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First());
    }

    public async Task<VndTaskCountsResponse> GetCountsAsync(int userId)
    {
        var coordinationCount = await _db.Set<VndApprovalStage>()
            .Where(s => s.ApproverUserId == userId)
            .Where(s =>
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.Primary
                 && s.PrimaryDecision == ApprovalStageDecision.Pending)
                ||
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.Repeated
                 && s.ParticipatesInRepeat
                 && (s.RepeatDecision == null || s.RepeatDecision == ApprovalStageDecision.Pending)))
            .CountAsync();

        var actualizationCount = (await GetActualizationTasksAsync(userId)).Count;
        var consolidationCount = (await GetConsolidationTasksAsync(userId)).Count;
        var myVndApprovalCount = (await GetMyVndApprovalTasksAsync(userId)).Count;

        return new VndTaskCountsResponse
        {
            Coordination = coordinationCount,
            Actualization = actualizationCount,
            Consolidation = consolidationCount,
            MyVndApproval = myVndApprovalCount
        };
    }

    /// <summary>Сводка персональных KPI для карточек на главной странице</summary>
    public async Task<VndHomeSummaryResponse> GetHomeSummaryAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Карточка 1: открытые циклы актуализации под моей ответственностью
        var myResponsibleActualizations = await _db.Set<VndActualizationRecord>()
            .CountAsync(r => r.ResponsibleUserId == userId && r.PublishedAt == null);

        // Карточка 2: решения, зачтённые мне по тайм-ауту, в текущем месяце
        // (Primary/Repeat/FinalHold — любая из трёх фаз, где я был согласующим)
        var myStages = await _db.Set<VndApprovalStage>()
            .Where(s => s.ApproverUserId == userId)
            .Select(s => new
            {
                s.PrimaryDecision, s.PrimaryDecidedAt,
                s.RepeatDecision, s.RepeatDecidedAt,
                s.FinalHoldDecision, s.FinalHoldDecidedAt
            })
            .ToListAsync();

        var myTimeoutApprovalsThisMonth = myStages.Count(s =>
            (s.PrimaryDecision == ApprovalStageDecision.AutoApprovedByTimeout
                && s.PrimaryDecidedAt.HasValue && s.PrimaryDecidedAt.Value >= monthStart)
            || (s.RepeatDecision == ApprovalStageDecision.AutoApprovedByTimeout
                && s.RepeatDecidedAt.HasValue && s.RepeatDecidedAt.Value >= monthStart)
            || (s.FinalHoldDecision == ApprovalStageDecision.AutoApprovedByTimeout
                && s.FinalHoldDecidedAt.HasValue && s.FinalHoldDecidedAt.Value >= monthStart));

        // Карточка 3: мои ВНД (я — инициатор согласования), процесс ещё не завершён
        var myVndAwaitingApproval = await _db.VndApprovalProcesses
            .CountAsync(p => p.InitiatorUserId == userId &&
                (p.Status == ApprovalProcessStatus.Primary
                 || p.Status == ApprovalProcessStatus.Repeated
                 || p.Status == ApprovalProcessStatus.RevisionNeeded
                 || p.Status == ApprovalProcessStatus.FinalHold));

        // Карточка 4: этапы, ожидающие решения именно меня прямо сейчас
        var pendingMyApproval = await GetCoordinationTasksAsync(userId);

        return new VndHomeSummaryResponse
        {
            MyResponsibleActualizations = myResponsibleActualizations,
            MyTimeoutApprovalsThisMonth = myTimeoutApprovalsThisMonth,
            MyVndAwaitingApproval = myVndAwaitingApproval,
            PendingMyApproval = pendingMyApproval.Count
        };
    }
}