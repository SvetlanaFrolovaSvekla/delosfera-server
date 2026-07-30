using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Models;
using delosfera_server.Modules.Files.Services;

namespace delosfera_server.Modules.Vnd.Services;

public class VndApprovalService : IVndApprovalService
{
    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _fileService;

    public VndApprovalService(DelosferaDbContext db, IFileStorageService fileService)
    {
        _db = db;
        _fileService = fileService;
    }

    public async Task<ApprovalProcessResponse> StartAsync(int vndId, StartApprovalRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var lastRedaction = await _db.VndRedactions
            .Where(r => r.VndId == vndId)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("У ВНД ещё нет ни одной редакции");

        if (lastRedaction.ApprovalStatus != RedactionApprovalStatus.Draft)
            throw new InvalidOperationException(
                "На согласование можно отправить только редакцию в статусе черновика (ещё не отправленную)");

        var alreadyRunning = await _db.VndApprovalProcesses
            .AnyAsync(x => x.RedactionId == lastRedaction.Id && x.Status != ApprovalProcessStatus.Approved
                                                              && x.Status != ApprovalProcessStatus.Cancelled);
        if (alreadyRunning)
            throw new InvalidOperationException("По этой редакции уже запущено согласование");

        if (request.PrimaryDeadlineHours <= 0 || request.RepeatDeadlineHours <= 0 || request.FinalHoldDeadlineHours <= 0)
            throw new InvalidOperationException("Все три норматива должны быть больше нуля часов");

        var stages = await BuildAndValidateStagesAsync(request.Stages);

        var process = new VndApprovalProcess
        {
            VndId = vndId,
            RedactionId = lastRedaction.Id,
            InitiatorUserId = currentUserId,
            Status = ApprovalProcessStatus.Primary,
            PrimaryDeadlineHours = request.PrimaryDeadlineHours,
            RepeatDeadlineHours = request.RepeatDeadlineHours,
            FinalHoldDeadlineHours = request.FinalHoldDeadlineHours,
            PrimaryStartedAt = DateTime.UtcNow,
            Stages = stages
        };

        _db.VndApprovalProcesses.Add(process);

        lastRedaction.ApprovalStatus = RedactionApprovalStatus.Pending;
        vnd.Status = VndStatus.Review;

        await _db.SaveChangesAsync();

        return await LoadResponseAsync(process.Id);
    }

    public async Task<ApprovalProcessResponse> GetByVndIdAsync(int vndId)
    {
        var lastRedaction = await _db.VndRedactions
            .Where(r => r.VndId == vndId)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"У ВНД с id={vndId} нет редакций");

        var process = await _db.VndApprovalProcesses
            .FirstOrDefaultAsync(x => x.RedactionId == lastRedaction.Id)
            ?? throw new KeyNotFoundException("Для последней редакции согласование не запускалось");

        return await LoadResponseAsync(process.Id);
    }

    public async Task<ApprovalProcessResponse> DecideAsync(
        int vndId, int stageId, ApprovalDecisionRequest request, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        var stage = process.Stages.FirstOrDefault(x => x.Id == stageId)
                    ?? throw new KeyNotFoundException($"Этап согласования с id={stageId} не найден");

        if (stage.ApproverUserId != currentUserId)
            throw new UnauthorizedAccessException("Вы не назначены согласующим на этом этапе");

        if ((request.Decision == ApprovalDecisionType.ApproveWithComment
             || request.Decision == ApprovalDecisionType.Reject)
            && string.IsNullOrWhiteSpace(request.Comment))
            throw new InvalidOperationException("Для этого решения необходимо оставить комментарий/сообщение");

        var decision = request.Decision switch
        {
            ApprovalDecisionType.Approve => ApprovalStageDecision.Approved,
            ApprovalDecisionType.ApproveWithComment => ApprovalStageDecision.ApprovedWithComment,
            ApprovalDecisionType.Reject => ApprovalStageDecision.Rejected,
            _ => throw new InvalidOperationException("Неизвестный тип решения")
        };

        switch (process.Status)
        {
            case ApprovalProcessStatus.Primary:
                if (stage.PrimaryDecision != ApprovalStageDecision.Pending)
                    throw new InvalidOperationException("Решение по первичному согласованию уже принято");

                stage.PrimaryDecision = decision;
                stage.PrimaryComment = request.Comment;
                stage.PrimaryDecidedAt = DateTime.UtcNow;
                stage.ParticipatesInRepeat = decision is ApprovalStageDecision.ApprovedWithComment
                                                       or ApprovalStageDecision.Rejected;

                await _db.SaveChangesAsync();

                if (process.Stages.All(s => s.PrimaryDecision != ApprovalStageDecision.Pending))
                    await CompletePrimaryPhaseAsync(process);
                break;

            case ApprovalProcessStatus.Repeated:
                if (!stage.ParticipatesInRepeat)
                    throw new InvalidOperationException("Этот согласующий не участвует в повторном согласовании");
                if (stage.RepeatDecision is not null && stage.RepeatDecision != ApprovalStageDecision.Pending)
                    throw new InvalidOperationException("Решение по повторному согласованию уже принято");

                stage.RepeatDecision = decision;
                stage.RepeatComment = request.Comment;
                stage.RepeatDecidedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                var repeatStages = process.Stages.Where(s => s.ParticipatesInRepeat).ToList();
                if (repeatStages.All(s => s.RepeatDecision is not null && s.RepeatDecision != ApprovalStageDecision.Pending))
                    await CompleteRepeatPhaseAsync(process);
                break;

            default:
                throw new InvalidOperationException(
                    "В текущем статусе процесса принятие решений недоступно");
        }

        return await LoadResponseAsync(process.Id);
    }

    public async Task<ApprovalProcessResponse> ResubmitAfterRevisionAsync(
        int vndId, ResubmitAfterRevisionRequest request, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        if (process.Status != ApprovalProcessStatus.RevisionNeeded)
            throw new InvalidOperationException(
                "Повторную отправку можно сделать только из статуса \"требуются правки\"");

        if (process.InitiatorUserId != currentUserId)
            throw new UnauthorizedAccessException("Отправить на повторное согласование может только инициатор");

        var redaction = process.Redaction!;

        if (request.DocRu is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocRu, currentUserId);
            redaction.DocFileRuId = saved.Id;
        }
        if (request.DocKg is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocKg, currentUserId);
            redaction.DocFileKgId = saved.Id;
        }
        if (request.DocEn is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocEn, currentUserId);
            redaction.DocFileEnId = saved.Id;
        }

        foreach (var stage in process.Stages.Where(s => s.ParticipatesInRepeat))
        {
            stage.RepeatDecision = ApprovalStageDecision.Pending;
            stage.RepeatComment = null;
            stage.RepeatDecidedAt = null;
        }

        process.Status = ApprovalProcessStatus.Repeated;
        process.RepeatStartedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await LoadResponseAsync(process.Id);
    }

    public async Task ProcessTimeoutsAsync()
    {
        var now = DateTime.UtcNow;

        // --- Первичный этап
        var primaryProcesses = await _db.VndApprovalProcesses
            .Include(x => x.Stages)
            .Include(x => x.Redaction)
            .Include(x => x.Vnd)
            .Where(x => x.Status == ApprovalProcessStatus.Primary)
            .ToListAsync();

        foreach (var process in primaryProcesses.Where(p => p.PrimaryDeadlineAt <= now))
        {
            foreach (var stage in process.Stages.Where(s => s.PrimaryDecision == ApprovalStageDecision.Pending))
            {
                stage.PrimaryDecision = ApprovalStageDecision.AutoApprovedByTimeout;
                stage.PrimaryDecidedAt = now;
            }
            await CompletePrimaryPhaseAsync(process, save: false);
        }

        // --- Повторный этап
        var repeatedProcesses = await _db.VndApprovalProcesses
            .Include(x => x.Stages)
            .Include(x => x.Redaction)
            .Include(x => x.Vnd)
            .Where(x => x.Status == ApprovalProcessStatus.Repeated)
            .ToListAsync();

        foreach (var process in repeatedProcesses.Where(p => p.RepeatDeadlineAt is not null && p.RepeatDeadlineAt <= now))
        {
            foreach (var stage in process.Stages.Where(s =>
                         s.ParticipatesInRepeat &&
                         (s.RepeatDecision is null || s.RepeatDecision == ApprovalStageDecision.Pending)))
            {
                stage.RepeatDecision = ApprovalStageDecision.AutoApprovedByTimeout;
                stage.RepeatDecidedAt = now;
            }
            await CompleteRepeatPhaseAsync(process, save: false);
        }

        // --- Финальная выдержка
        var finalHoldProcesses = await _db.VndApprovalProcesses
            .Include(x => x.Redaction)
            .Include(x => x.Vnd)
            .Where(x => x.Status == ApprovalProcessStatus.FinalHold)
            .ToListAsync();

        foreach (var process in finalHoldProcesses.Where(p => p.FinalHoldDeadlineAt is not null && p.FinalHoldDeadlineAt <= now))
        {
            FinalizeApproval(process);
        }

        await _db.SaveChangesAsync();
    }
    
    private async Task CompletePrimaryPhaseAsync(VndApprovalProcess process, bool save = true)
    {
        var hasRemarks = process.Stages.Any(s =>
            s.PrimaryDecision is ApprovalStageDecision.ApprovedWithComment or ApprovalStageDecision.Rejected);

        if (!hasRemarks)
        {
            // Если никто не оставил замечаний, то финальной выдержки не будет, ВНД сразу действующий
            FinalizeApproval(process);
        }
        else
        {
            process.Status = ApprovalProcessStatus.RevisionNeeded;
        }

        if (save) await _db.SaveChangesAsync();
    }

    private async Task CompleteRepeatPhaseAsync(VndApprovalProcess process, bool save = true)
    {
        process.Status = ApprovalProcessStatus.FinalHold;
        process.FinalHoldStartedAt = DateTime.UtcNow;

        if (save) await _db.SaveChangesAsync();
    }

    private void FinalizeApproval(VndApprovalProcess process)
    {
        process.Status = ApprovalProcessStatus.Approved;
        process.CompletedAt = DateTime.UtcNow;

        var redaction = process.Redaction!;
        var vnd = process.Vnd!;

        redaction.ApprovalStatus = RedactionApprovalStatus.Approved;
        vnd.CurrentRedactionId = redaction.Id;
        vnd.RevisionChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        vnd.Status = VndStatus.Active;
    }

    private async Task<List<VndApprovalStage>> BuildAndValidateStagesAsync(List<ApprovalStageRequest> requestStages)
    {
        if (requestStages.Count < 4)
            throw new InvalidOperationException(
                "Маршрут должен содержать минимум 4 этапа: Юр. управление, Риск-менеджмент, Комплаенс и Методология");

        if (requestStages[0].Kind != ApprovalStageKind.Legal)
            throw new InvalidOperationException("Первый этап маршрута всегда — Юридическое управление");
        if (requestStages[1].Kind != ApprovalStageKind.RiskManagement)
            throw new InvalidOperationException("Второй этап маршрута всегда — Управление риск-менеджмента");
        if (requestStages[2].Kind != ApprovalStageKind.Compliance)
            throw new InvalidOperationException("Третий этап маршрута всегда — Управление комплаенс-контроля");
        if (requestStages[^1].Kind != ApprovalStageKind.Methodology)
            throw new InvalidOperationException("Последний этап маршрута всегда — Отдел методологии");

        for (var i = 3; i < requestStages.Count - 1; i++)
        {
            if (requestStages[i].Kind != ApprovalStageKind.Custom)
                throw new InvalidOperationException(
                    "Промежуточные этапы (между фиксированными) должны иметь тип Custom");
        }

        var approverIds = requestStages.Select(s => s.ApproverUserId).ToList();
        if (approverIds.Distinct().Count() != approverIds.Count)
            throw new InvalidOperationException("Один пользователь не может занимать два этапа одновременно");

        var users = await _db.Users.Where(u => approverIds.Contains(u.Id)).ToListAsync();
        var missing = approverIds.Except(users.Select(u => u.Id)).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Пользователи с id={string.Join(", ", missing)} не найдены");

        var usersById = users.ToDictionary(u => u.Id);

        var stages = new List<VndApprovalStage>();
        for (var i = 0; i < requestStages.Count; i++)
        {
            var reqStage = requestStages[i];
            var approver = usersById[reqStage.ApproverUserId];

            var expectedOrgUnitId = reqStage.Kind switch
            {
                ApprovalStageKind.Legal => FixedApprovalOrgUnits.LegalOrgUnitId,
                ApprovalStageKind.RiskManagement => FixedApprovalOrgUnits.RiskManagementOrgUnitId,
                ApprovalStageKind.Compliance => FixedApprovalOrgUnits.ComplianceOrgUnitId,
                ApprovalStageKind.Methodology => FixedApprovalOrgUnits.MethodologyOrgUnitId,
                _ => (int?)null
            };

            if (expectedOrgUnitId.HasValue && approver.OrgUnitId != expectedOrgUnitId)
                throw new InvalidOperationException(
                    $"Согласующий на этапе {i + 1} ({reqStage.Kind}) должен относиться к соответствующему подразделению");

            stages.Add(new VndApprovalStage
            {
                Order = i + 1,
                Kind = reqStage.Kind,
                OrgUnitId = approver.OrgUnitId ?? expectedOrgUnitId
                            ?? throw new InvalidOperationException("У согласующего не указано подразделение"),
                ApproverUserId = approver.Id
            });
        }

        return stages;
    }

    private async Task<VndApprovalProcess> LoadProcessForVndAsync(int vndId)
    {
        var lastRedaction = await _db.VndRedactions
            .Where(r => r.VndId == vndId)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"У ВНД с id={vndId} нет редакций");

        return await _db.VndApprovalProcesses
            .Include(x => x.Stages)
            .Include(x => x.Redaction)
            .Include(x => x.Vnd)
            .FirstOrDefaultAsync(x => x.RedactionId == lastRedaction.Id)
            ?? throw new KeyNotFoundException("Для последней редакции согласование не запускалось");
    }

    private async Task<ApprovalProcessResponse> LoadResponseAsync(int processId)
    {
        var process = await _db.VndApprovalProcesses
            .Include(x => x.Stages).ThenInclude(s => s.OrgUnit)
            .Include(x => x.Stages).ThenInclude(s => s.ApproverUser)
            .FirstAsync(x => x.Id == processId);

        var initiator = await _db.Users.FindAsync(process.InitiatorUserId);

        return new ApprovalProcessResponse
        {
            Id = process.Id,
            VndId = process.VndId,
            RedactionId = process.RedactionId,
            InitiatorUserId = process.InitiatorUserId,
            InitiatorName = initiator?.FullName ?? "",
            Status = MapStatus(process.Status),
            PrimaryDeadlineHours = process.PrimaryDeadlineHours,
            RepeatDeadlineHours = process.RepeatDeadlineHours,
            FinalHoldDeadlineHours = process.FinalHoldDeadlineHours,
            PrimaryStartedAt = process.PrimaryStartedAt,
            PrimaryDeadlineAt = process.PrimaryDeadlineAt,
            RepeatStartedAt = process.RepeatStartedAt,
            RepeatDeadlineAt = process.RepeatDeadlineAt,
            FinalHoldStartedAt = process.FinalHoldStartedAt,
            FinalHoldDeadlineAt = process.FinalHoldDeadlineAt,
            CompletedAt = process.CompletedAt,
            CreatedAt = process.CreatedAt,
            UpdatedAt = process.UpdatedAt,
            Stages = process.Stages.OrderBy(s => s.Order).Select(s => new ApprovalStageResponse
            {
                Id = s.Id,
                Order = s.Order,
                Kind = MapKind(s.Kind),
                OrgUnitId = s.OrgUnitId,
                OrgUnitName = s.OrgUnit?.TitleRu ?? "",
                ApproverUserId = s.ApproverUserId,
                ApproverName = s.ApproverUser?.FullName ?? "",
                PrimaryDecision = MapDecision(s.PrimaryDecision),
                PrimaryComment = s.PrimaryComment,
                PrimaryDecidedAt = s.PrimaryDecidedAt,
                ParticipatesInRepeat = s.ParticipatesInRepeat,
                RepeatDecision = s.RepeatDecision.HasValue ? MapDecision(s.RepeatDecision.Value) : null,
                RepeatComment = s.RepeatComment,
                RepeatDecidedAt = s.RepeatDecidedAt
            }).ToList()
        };
    }

    private static string MapStatus(ApprovalProcessStatus status) => status switch
    {
        ApprovalProcessStatus.Primary => "primary",
        ApprovalProcessStatus.RevisionNeeded => "revision_needed",
        ApprovalProcessStatus.Repeated => "repeated",
        ApprovalProcessStatus.FinalHold => "final_hold",
        ApprovalProcessStatus.Approved => "approved",
        ApprovalProcessStatus.Cancelled => "cancelled",
        _ => "primary"
    };

    private static string MapKind(ApprovalStageKind kind) => kind switch
    {
        ApprovalStageKind.Legal => "legal",
        ApprovalStageKind.RiskManagement => "risk_management",
        ApprovalStageKind.Compliance => "compliance",
        ApprovalStageKind.Custom => "custom",
        ApprovalStageKind.Methodology => "methodology",
        _ => "custom"
    };

    private static string MapDecision(ApprovalStageDecision decision) => decision switch
    {
        ApprovalStageDecision.Pending => "pending",
        ApprovalStageDecision.Approved => "approved",
        ApprovalStageDecision.ApprovedWithComment => "approved_with_comment",
        ApprovalStageDecision.Rejected => "rejected",
        ApprovalStageDecision.AutoApprovedByTimeout => "auto_approved_timeout",
        _ => "pending"
    };
}