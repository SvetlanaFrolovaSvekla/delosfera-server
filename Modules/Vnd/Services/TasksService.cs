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
            DeadlineHours = isPrimaryPhase ? process.PrimaryDeadlineHours : process.RepeatDeadlineHours,
            CreatedAt = isPrimaryPhase ? process.PrimaryStartedAt : (process.RepeatStartedAt ?? process.CreatedAt)
        };
    })
    .OrderBy(t => t.DeadlineAt)
    .ToList();
}

    public Task<List<VndTaskResponse>> GetActualizationTasksAsync(int userId) =>
        GetByStatusForUserAsync(userId, VndStatus.OnActualization, "actualization");

    public Task<List<VndTaskResponse>> GetConsolidationTasksAsync(int userId) =>
        GetByStatusForUserAsync(userId, VndStatus.Consolidation, "consolidation");

    private async Task<List<VndTaskResponse>> GetByStatusForUserAsync(int userId, VndStatus status, string scope)
    {
        var docs = await _db.VndDocuments
            .Include(x => x.ResponsibleExecutors)
            .Where(x => x.Status == status)
            .Where(x =>
                x.DeveloperId == userId
                || x.CuratorDeveloperId == userId
                || x.ResponsibleExecutors.Any(e => e.CuratorUserId == userId))
            .ToListAsync();

        return docs.Select(x => new VndTaskResponse
        {
            VndId = x.Id,
            VndCode = x.Code,
            VndTitle = x.TitleRu,
            Scope = scope,
            DueActualizationDate = x.DueActualizationDate,
            CreatedAt = x.UpdatedAt
        })
        .OrderBy(t => t.DueActualizationDate)
        .ToList();
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

        var actualizationCount = await CountByStatusForUserAsync(userId, VndStatus.OnActualization);
        var consolidationCount = await CountByStatusForUserAsync(userId, VndStatus.Consolidation);

        return new VndTaskCountsResponse
        {
            Coordination = coordinationCount,
            Actualization = actualizationCount,
            Consolidation = consolidationCount
        };
    }

    private async Task<int> CountByStatusForUserAsync(int userId, VndStatus status) =>
        await _db.VndDocuments
            .Where(x => x.Status == status)
            .Where(x =>
                x.DeveloperId == userId
                || x.CuratorDeveloperId == userId
                || x.ResponsibleExecutors.Any(e => e.CuratorUserId == userId))
            .CountAsync();
    
}

