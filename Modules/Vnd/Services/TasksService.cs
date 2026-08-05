using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Models;

namespace delosfera_server.Modules.Vnd.Services;

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

    // Инициаторов подтягиваем одним запросом, чтобы не делать N+1
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

    /// <summary>ВНД с открытым (ещё не опубликованным) циклом актуализации — остаётся в списке,
    /// пока PublishAsync не проставит PublishedAt, независимо от того, через какие статусы
    /// (OnActualization -> Review -> Consolidation) документ успел пройти за это время.</summary>
    public async Task<List<VndTaskResponse>> GetActualizationTasksAsync(int userId)
    {
        var openVndIds = await GetOpenActualizationVndIdsAsync();
        if (openVndIds.Count == 0) return new List<VndTaskResponse>();

        var docs = await _db.VndDocuments
            .Include(x => x.ResponsibleExecutors)
            .Where(x => openVndIds.Contains(x.Id))
            .Where(x =>
                x.DeveloperId == userId
                || x.CuratorDeveloperId == userId
                || x.ActualizationResponsibleUserId == userId
                || x.ResponsibleExecutors.Any(e => e.CuratorUserId == userId))
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

    /// <summary>ВНД в статусе консолидации. Аудитория — прежние критерии (разработчик/куратор/
    /// куратор ответственного исполнителя) плюс инициатор согласования и ответственный за актуализацию.</summary>
    public async Task<List<VndTaskResponse>> GetConsolidationTasksAsync(int userId)
    {
        var docs = await _db.VndDocuments
            .Include(x => x.ResponsibleExecutors)
            .Where(x => x.Status == VndStatus.Consolidation)
            .ToListAsync();

        if (docs.Count == 0) return new List<VndTaskResponse>();

        var vndIds = docs.Select(x => x.Id).ToList();
        var initiatorByVndId = await GetCurrentApprovalInitiatorsByVndIdAsync(vndIds);

        var filtered = docs.Where(x =>
                x.DeveloperId == userId
                || x.CuratorDeveloperId == userId
                || x.ResponsibleExecutors.Any(e => e.CuratorUserId == userId)
                || x.ActualizationResponsibleUserId == userId
                || (initiatorByVndId.TryGetValue(x.Id, out var initiatorId) && initiatorId == userId))
            .ToList();

        return filtered.Select(x => new VndTaskResponse
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

    /// <summary>Мои ВНД на согласовании — ВНД в статусе Review, где текущий пользователь
    /// является инициатором активного (незавершённого) процесса согласования либо
    /// ответственным за текущий цикл актуализации. Аудитория отличается от GetCoordinationTasksAsync
    /// (там — согласующие), поэтому дублирование одного и того же ВНД в разных вкладках ожидаемо.</summary>
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

    /// <summary>Id ВНД, у которых есть открытый (ещё не опубликованный) цикл актуализации</summary>
    private async Task<HashSet<int>> GetOpenActualizationVndIdsAsync(List<int>? restrictToVndIds = null)
    {
        var query = _db.Set<VndActualizationRecord>().Where(r => r.PublishedAt == null);
        if (restrictToVndIds is not null)
            query = query.Where(r => restrictToVndIds.Contains(r.VndId));

        var ids = await query.Select(r => r.VndId).Distinct().ToListAsync();
        return ids.ToHashSet();
    }

    /// <summary>Текущий (незавершённый) процесс согласования для каждого из переданных ВНД.
    /// Незавершённый = статус не Approved/Cancelled/Rejected. Если процессов несколько
    /// (в норме не должно быть), берётся самый свежий по CreatedAt.</summary>
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

    private async Task<Dictionary<int, int>> GetCurrentApprovalInitiatorsByVndIdAsync(List<int> vndIds)
    {
        var processByVndId = await GetCurrentApprovalProcessesByVndIdAsync(vndIds);
        return processByVndId.ToDictionary(kv => kv.Key, kv => kv.Value.InitiatorUserId);
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
}

