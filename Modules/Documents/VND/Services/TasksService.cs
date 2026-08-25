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

    /// <summary>Задачи вкладки "Ждущие моего согласования" — все три фазы, на которых
    /// решение сейчас за текущим пользователем: первичное, повторное согласование и
    /// финальная выдержка. Раньше финальная выдержка сюда не попадала — по переходу в этот
    /// статус согласующим уходило только уведомление (см. WorkflowNotifier/VndApprovalService),
    /// а сама задача в "Мои задачи" не появлялась.</summary>
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
                 && (s.RepeatDecision == null || s.RepeatDecision == ApprovalStageDecision.Pending))
                ||
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.FinalHold
                 && (s.FinalHoldDecision == null || s.FinalHoldDecision == ApprovalStageDecision.Pending)))
            .ToListAsync();

        var initiators = await GetInitiatorNamesAsync(stages.Select(s => s.ApprovalProcess!.InitiatorUserId));

        return stages.Select(s =>
        {
            var process = s.ApprovalProcess!;
            var phase = MapProcessPhase(process.Status);

            return new VndTaskResponse
            {
                VndId = process.VndId,
                VndCode = process.Vnd!.Code,
                VndTitle = process.Vnd!.TitleRu,
                Scope = "coordination",
                VndStatus = MapVndStatus(process.Vnd!.Status),
                RedactionId = process.RedactionId,
                RedactionCode = process.Redaction!.Code,
                StageId = s.Id,
                StagePhase = phase,
                StageKind = MapStageKind(s.Kind),
                DeadlineAt = phase switch
                {
                    "primary" => process.PrimaryDeadlineAt,
                    "repeat" => process.RepeatDeadlineAt,
                    "final" => process.FinalHoldDeadlineAt,
                    _ => null
                },
                InitiatorName = initiators.GetValueOrDefault(process.InitiatorUserId, "—"),
                DeadlineMinutes = phase switch
                {
                    "primary" => process.PrimaryDeadlineMinutes,
                    "repeat" => process.RepeatDeadlineMinutes,
                    "final" => process.FinalHoldDeadlineMinutes,
                    _ => null
                },
                InitiatorComment = phase == "primary" ? null : process.RepeatInitiatorComment,
                ActualizationPlannedNoChanges = process.Vnd!.ActualizationPlannedNoChanges,
                CreatedAt = phase switch
                {
                    "primary" => process.PrimaryStartedAt,
                    "repeat" => process.RepeatStartedAt ?? process.CreatedAt,
                    "final" => process.FinalHoldStartedAt ?? process.CreatedAt,
                    _ => process.CreatedAt
                }
            };
        })
        .OrderBy(t => t.DeadlineAt)
        .ToList();
    }

    /// <summary>Видит ответственный за актуализацию этого конкретного цикла
    /// (VndDocument.ActualizationResponsibleUserId) — раньше здесь ошибочно фильтровалось по
    /// CreatedByUserId (создателю ВНД), из-за чего назначенный ответственный (если это не он
    /// сам создавал документ) вообще не видел задачу о необходимости актуализировать.</summary>
    public async Task<List<VndTaskResponse>> GetActualizationTasksAsync(int userId)
    {
        var openVndIds = await GetOpenActualizationVndIdsAsync();
        if (openVndIds.Count == 0) return new List<VndTaskResponse>();

        // Статус тоже фильтруем, а не только "цикл ещё не опубликован" (PublishedAt == null) —
        // иначе документ, дошедший до Consolidation в рамках того же открытого цикла, продолжает
        // висеть здесь ОДНОВРЕМЕННО с задачей в GetConsolidationTasksAsync: пользователь видит
        // два "дубликата" одной и той же работы, причём актуализационная карточка выглядит
        // "свежее" из-за собственной сортировки/CreatedAt, хотя по факту документ уже ушёл дальше.
        var docs = await _db.VndDocuments
            .Where(x => openVndIds.Contains(x.Id)
                        && x.ActualizationResponsibleUserId == userId
                        && x.Status == VndStatus.OnActualization)
            .ToListAsync();

        return docs.Select(x => new VndTaskResponse
            {
                VndId = x.Id,
                VndCode = x.Code,
                VndTitle = x.TitleRu,
                Scope = "actualization",
                VndStatus = MapVndStatus(x.Status),
                DueActualizationDate = x.DueActualizationDate,
                ActualizationPlannedNoChanges = x.ActualizationPlannedNoChanges,
                ActualizationPerformed = x.ActualizationPerformed,
                CreatedAt = x.UpdatedAt
            })
            .OrderBy(t => t.DueActualizationDate)
            .ToList();
    }

    /// <summary>Видит ответственный за актуализацию этого цикла
    /// (VndDocument.ActualizationResponsibleUserId) — та же поправка, что и для актуализации.
    /// Если консолидация не связана с циклом актуализации (обычное согласование первой редакции,
    /// ActualizationResponsibleUserId == null), задачу по-прежнему видит инициатор.</summary>
    public async Task<List<VndTaskResponse>> GetConsolidationTasksAsync(int userId)
    {
        var docs = await _db.VndDocuments
            .Where(x => x.Status == VndStatus.Consolidation
                        && (x.ActualizationResponsibleUserId == userId
                            || (x.ActualizationResponsibleUserId == null && x.CreatedByUserId == userId)))
            .ToListAsync();

        if (docs.Count == 0) return new List<VndTaskResponse>();

        return docs.Select(x => new VndTaskResponse
            {
                VndId = x.Id,
                VndCode = x.Code,
                VndTitle = x.TitleRu,
                Scope = "consolidation",
                VndStatus = MapVndStatus(x.Status),
                StatusLabel = "В процессе консолидации",
                DueActualizationDate = x.DueActualizationDate,
                ActualizationPlannedNoChanges = x.ActualizationPlannedNoChanges,
                ActualizationPerformed = x.ActualizationPerformed,
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
                VndStatus = MapVndStatus(vnd.Status),
                RedactionId = process.RedactionId,
                RedactionCode = process.Redaction?.Code,
                // Текущий этап согласования — тот же смысл, что и у "Ждущих моего согласования",
                // только с точки зрения инициатора: на каком именно круге сейчас его редакция.
                // Для RevisionNeeded (на доработке у самого инициатора) фазы нет ни в одном из
                // трёх согласованных значений — фильтр по этапу её не подхватит, "Все этапы" покажет.
                StagePhase = MapProcessPhase(process.Status),
                StatusLabel = statusLabel,
                ActualizationPlannedNoChanges = vnd.ActualizationPlannedNoChanges,
                CreatedAt = vnd.UpdatedAt
            });
        }

        return result.OrderBy(t => t.CreatedAt).ToList();
    }

    private async Task<Dictionary<int, string>> GetInitiatorNamesAsync(IEnumerable<int> initiatorUserIds)
    {
        var ids = initiatorUserIds.Distinct().ToList();
        return await _db.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);
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
                 && (s.RepeatDecision == null || s.RepeatDecision == ApprovalStageDecision.Pending))
                ||
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.FinalHold
                 && (s.FinalHoldDecision == null || s.FinalHoldDecision == ApprovalStageDecision.Pending)))
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
        // (первичное/повторное согласование + финальная выдержка — все три уже внутри
        // GetCoordinationTasksAsync)
        var pendingMyApproval = await GetCoordinationTasksAsync(userId);

        return new VndHomeSummaryResponse
        {
            MyResponsibleActualizations = myResponsibleActualizations,
            MyTimeoutApprovalsThisMonth = myTimeoutApprovalsThisMonth,
            MyVndAwaitingApproval = myVndAwaitingApproval,
            PendingMyApproval = pendingMyApproval.Count
        };
    }

    // ── маппинг enum → строковые ключи для фронта ──────────────────────────

    /// <summary>"primary" | "repeat" | "final" — фаза согласования, под которую заточен и
    /// фильтр "Этап согласования" на странице "Мои задачи", и бейдж на карточке.
    /// RevisionNeeded (доработка у инициатора) не сопоставляется ни с одной из трёх фаз.</summary>
    private static string? MapProcessPhase(ApprovalProcessStatus status) => status switch
    {
        ApprovalProcessStatus.Primary => "primary",
        ApprovalProcessStatus.Repeated => "repeat",
        ApprovalProcessStatus.FinalHold => "final",
        _ => null
    };

    private static string MapStageKind(ApprovalStageKind kind) => kind switch
    {
        ApprovalStageKind.Legal => "legal",
        ApprovalStageKind.RiskManagement => "risk_management",
        ApprovalStageKind.Compliance => "compliance",
        ApprovalStageKind.Custom => "custom",
        ApprovalStageKind.Methodology => "methodology",
        _ => "custom"
    };

    private static string MapVndStatus(VndStatus status) => status switch
    {
        VndStatus.Active => "active",
        VndStatus.OnActualization => "onact",
        VndStatus.Review => "review",
        VndStatus.Consolidation => "consol",
        VndStatus.Archived => "arch",
        VndStatus.Draft => "draft",
        _ => "active"
    };
}
