using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Notifications;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public class VndApprovalService : IVndApprovalService
{
    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _fileService;
    private readonly INotificationService _notifications;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VndApprovalService> _logger;

    public VndApprovalService(
        DelosferaDbContext db,
        IFileStorageService fileService,
        INotificationService notifications,
        ICurrentUserService currentUser,
        ILogger<VndApprovalService> logger)
    {
        _db = db;
        _fileService = fileService;
        _notifications = notifications;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ApprovalProcessResponse> StartAsync(int vndId, StartApprovalRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var isChiefEditor = _currentUser.HasPermission(PermissionCode.CreateVndWithApproval)
                            || _currentUser.HasPermission(PermissionCode.CreateVndWithoutApproval)
                            || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval)
                            || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);

        if (!isChiefEditor && !await IsLinkedToVndAsync(vnd, currentUserId))
            throw new UnauthorizedAccessException(
                "Запустить согласование может только разработчик, куратор, ответственный исполнитель, " +
                "ответственный за актуализацию или главный редактор ВНД");

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

        if (request.PrimaryDeadlineMinutes <= 0 || request.RepeatDeadlineMinutes <= 0 ||
            request.FinalHoldDeadlineMinutes <= 0)
            throw new InvalidOperationException("Все три норматива должны быть больше нуля минут");

        // Себя можно указать согласующим только на фиксированном этапе (Legal/RiskManagement/
        // Compliance/Methodology) - принадлежность инициатора нужному подразделению всё равно
        // проверяется ниже в BuildAndValidateStagesAsync. На дополнительных (Custom) этапах,
        // которые инициатор сам добавил, себя указывать нельзя.
        if (request.Stages.Any(s => s.Kind == ApprovalStageKind.Custom && s.ApproverUserId == currentUserId))
            throw new InvalidOperationException(
                "Вы не можете быть согласующим на дополнительном этапе, который сами добавили");

        var stages = await BuildAndValidateStagesAsync(request.Stages);

        // Этапы, где согласующий - сам инициатор (фиксированный этап его же подразделения),
        // считаем согласованными автоматически, без ожидания решения.
        var now = DateTime.UtcNow;
        foreach (var selfStage in stages.Where(s => s.ApproverUserId == currentUserId))
        {
            selfStage.PrimaryDecision = ApprovalStageDecision.Approved;
            selfStage.PrimaryComment = "Согласовано автоматически — инициатор является согласующим на этом этапе";
            selfStage.PrimaryDecidedAt = now;
        }

        var process = new VndApprovalProcess
        {
            VndId = vndId,
            RedactionId = lastRedaction.Id,
            InitiatorUserId = currentUserId,
            Status = ApprovalProcessStatus.Primary,
            PrimaryDeadlineMinutes = request.PrimaryDeadlineMinutes,
            RepeatDeadlineMinutes = request.RepeatDeadlineMinutes,
            FinalHoldDeadlineMinutes = request.FinalHoldDeadlineMinutes,
            PrimaryStartedAt = now,
            Stages = stages
        };

        _db.VndApprovalProcesses.Add(process);

        lastRedaction.ApprovalStatus = RedactionApprovalStatus.Pending;
        vnd.Status = VndStatus.Review;

        await _db.SaveChangesAsync();

        // --- Уведомления: задача на первичное согласование - только тем, кому реально нужно
        // принять решение (этапы, автоматически согласованные самим инициатором, исключаем)
        var pendingApproverIds = stages
            .Where(s => s.ApproverUserId != currentUserId)
            .Select(s => s.ApproverUserId)
            .ToArray();

        if (pendingApproverIds.Length > 0)
            await NotifyAsync(
                VndApprovalNotificationMessages.TaskPrimaryApproval(lastRedaction.Code, vnd.TitleRu),
                NotificationCategory.Approval, vndId, currentUserId, pendingApproverIds);

        // --- Уведомление инициатору (и ответственному за актуализацию, если согласование запущено
        // в рамках открытого цикла актуализации): редакция отправлена на согласование
        var sentToApprovalRecipients = new List<int> { currentUserId };
        if (vnd.ActualizationResponsibleUserId.HasValue)
            sentToApprovalRecipients.Add(vnd.ActualizationResponsibleUserId.Value);

        await NotifyAsync(
            VndApprovalNotificationMessages.SentToApproval(lastRedaction.Code, vnd.TitleRu),
            NotificationCategory.Approval, vndId, currentUserId,
            sentToApprovalRecipients.ToArray());

        // --- Если абсолютно все этапы оказались автоматически согласованы инициатором
        // (маловероятно, но возможно на коротком маршруте) - первичная фаза уже завершена,
        // сразу проверяем дальнейший переход (RevisionNeeded/Consolidation).
        if (process.Stages.All(s => s.PrimaryDecision != ApprovalStageDecision.Pending))
            await CompletePrimaryPhaseAsync(process);

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
                if (repeatStages.All(s =>
                        s.RepeatDecision is not null && s.RepeatDecision != ApprovalStageDecision.Pending))
                    await CompleteRepeatPhaseAsync(process);
                break;

            case ApprovalProcessStatus.FinalHold:
                if (stage.FinalHoldDecision is not null && stage.FinalHoldDecision != ApprovalStageDecision.Pending)
                    throw new InvalidOperationException("Решение по финальной выдержке уже принято");

                stage.FinalHoldDecision = decision;
                stage.FinalHoldComment = request.Comment;
                stage.FinalHoldDecidedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                if (decision is ApprovalStageDecision.ApprovedWithComment or ApprovalStageDecision.Rejected)
                {
                    // Замечание на финальной выдержке - возвращаем на доработку.
                    // Матрица разногласий предыдущего круга сохраняется как есть.
                    await ReturnToRevisionFromFinalHoldAsync(process);
                    await _db.SaveChangesAsync();
                }
                else if (process.Stages.All(s =>
                             s.FinalHoldDecision is not null && s.FinalHoldDecision != ApprovalStageDecision.Pending))
                {
                    await FinalizeApprovalAsync(process, afterRevision: true);
                    await _db.SaveChangesAsync();
                }

                break;

            default:
                throw new InvalidOperationException(
                    "В текущем статусе процесса принятие решений недоступно");
        }

        // --- Уведомление инициатору о конкретном решении согласующего
        // (общее для первичного, повторного и финального этапов - decision уже посчитан выше)
        var approver = await _db.Users.FindAsync(currentUserId);
        var approverName = approver?.FullName ?? "—";
        var redactionCode = process.Redaction!.Code;
        var vndTitle = process.Vnd!.TitleRu;

        var decisionNotice = decision switch
        {
            ApprovalStageDecision.Approved =>
                VndApprovalNotificationMessages.ApprovedByUser(approverName, redactionCode, vndTitle),
            ApprovalStageDecision.ApprovedWithComment =>
                VndApprovalNotificationMessages.ApprovedWithComment(approverName, redactionCode, vndTitle,
                    request.Comment),
            ApprovalStageDecision.Rejected =>
                VndApprovalNotificationMessages.Rejected(approverName, redactionCode, vndTitle, request.Comment),
            _ => throw new InvalidOperationException($"Неожиданное значение decision: {decision}")
        };

        await NotifyAsync(
            decisionNotice, NotificationCategory.Approval, vndId, currentUserId, process.InitiatorUserId);

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

        if (!request.AgreesWithAllRemarks && process.DisagreementMatrixRows.Count == 0)
            throw new InvalidOperationException(
                "Если вы не согласны со всеми замечаниями, заполните матрицу разногласий (хотя бы одна строка)");

        var redaction = process.Redaction!;

        // ТИД обязателен на каждом круге доработки, если он был обязателен при первичной подаче
        // редакции (Number > 1 - значит документ актуализируется, а не создаётся впервые).
        var requiresTid = redaction.Number > 1;
        if (requiresTid && request.Tid is null)
            throw new InvalidOperationException(
                "При актуализации ВНД необходимо приложить обновлённый файл ТИД вместе с исправленной редакцией");

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

        if (request.Tid is not null)
        {
            var saved = await _fileService.SaveAsync(request.Tid, currentUserId);
            redaction.TidFileId = saved.Id;
        }

        process.RepeatInitiatorComment = request.Comment;

        if (request.AgreesWithAllRemarks)
        {
            // Замечания исправлены - обычное повторное согласование (только с теми, кто участвует в repeat)
            foreach (var stage in process.Stages.Where(s => s.ParticipatesInRepeat))
            {
                stage.RepeatDecision = ApprovalStageDecision.Pending;
                stage.RepeatComment = null;
                stage.RepeatDecidedAt = null;
            }

            process.Status = ApprovalProcessStatus.Repeated;
            process.RepeatStartedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var repeatApproverIds = process.Stages
                .Where(s => s.ParticipatesInRepeat)
                .Select(s => s.ApproverUserId)
                .ToArray();

            await NotifyAsync(
                VndApprovalNotificationMessages.TaskRepeatApproval(redaction.Code, process.Vnd!.TitleRu),
                NotificationCategory.Approval, vndId, currentUserId, repeatApproverIds);
        }
        else
        {
            // Составлена матрица разногласий - повторное согласование пропускаем,
            // сразу идём на финальную выдержку (там решения принимают ВСЕ этапы)
            foreach (var stage in process.Stages)
            {
                stage.FinalHoldDecision = ApprovalStageDecision.Pending;
                stage.FinalHoldComment = null;
                stage.FinalHoldDecidedAt = null;
            }

            process.Status = ApprovalProcessStatus.FinalHold;
            process.FinalHoldStartedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var stageApproverIds = process.Stages.Select(s => s.ApproverUserId).ToArray();

            await NotifyAsync(
                VndApprovalNotificationMessages.FinalHoldForApprovers(redaction.Code, process.Vnd!.TitleRu),
                NotificationCategory.Approval, vndId, currentUserId, stageApproverIds);

            await NotifyAsync(
                VndApprovalNotificationMessages.SentToFinalHold(redaction.Code),
                NotificationCategory.Approval, vndId, currentUserId, currentUserId);
        }

        return await LoadResponseAsync(process.Id);
    }

    public async Task<DisagreementMatrixRowResponse> AddDisagreementMatrixRowAsync(
        int vndId, AddDisagreementMatrixRowRequest request, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        if (process.InitiatorUserId != currentUserId)
            throw new UnauthorizedAccessException("Заполнять матрицу разногласий может только инициатор");

        if (process.Status != ApprovalProcessStatus.RevisionNeeded)
            throw new InvalidOperationException(
                "Матрицу разногласий можно заполнять только в статусе \"требуются правки\"");

        var row = new VndDisagreementMatrixRow
        {
            ApprovalProcessId = process.Id,
            DeveloperPosition = request.DeveloperPosition,
            OpponentPosition = request.OpponentPosition,
            DeveloperJustification = request.DeveloperJustification,
            CreatedByUserId = currentUserId
        };

        _db.Set<VndDisagreementMatrixRow>().Add(row);
        await _db.SaveChangesAsync();

        return ToDisagreementRowResponse(row);
    }

    public async Task DeleteDisagreementMatrixRowAsync(int vndId, int rowId, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        if (process.InitiatorUserId != currentUserId)
            throw new UnauthorizedAccessException("Удалять строки матрицы разногласий может только инициатор");

        if (process.Status != ApprovalProcessStatus.RevisionNeeded)
            throw new InvalidOperationException(
                "Матрицу разногласий можно редактировать только в статусе \"требуются правки\"");

        var row = await _db.Set<VndDisagreementMatrixRow>()
                      .FirstOrDefaultAsync(x => x.Id == rowId && x.ApprovalProcessId == process.Id)
                  ?? throw new KeyNotFoundException($"Строка матрицы разногласий с id={rowId} не найдена");

        _db.Set<VndDisagreementMatrixRow>().Remove(row);
        await _db.SaveChangesAsync();
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

        foreach (var process in
                 repeatedProcesses.Where(p => p.RepeatDeadlineAt is not null && p.RepeatDeadlineAt <= now))
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
            .Include(x => x.Stages)
            .Include(x => x.Redaction)
            .Include(x => x.Vnd)
            .Where(x => x.Status == ApprovalProcessStatus.FinalHold)
            .ToListAsync();

        foreach (var process in finalHoldProcesses.Where(p =>
                     p.FinalHoldDeadlineAt is not null && p.FinalHoldDeadlineAt <= now))
        {
            foreach (var stage in process.Stages.Where(s =>
                         s.FinalHoldDecision is null || s.FinalHoldDecision == ApprovalStageDecision.Pending))
            {
                stage.FinalHoldDecision = ApprovalStageDecision.AutoApprovedByTimeout;
                stage.FinalHoldDecidedAt = now;
            }

            // Никто не оставил замечаний до дедлайна - завершаем (afterRevision: true,
            // т.к. финальная выдержка бывает только после цикла с замечаниями)
            await FinalizeApprovalAsync(process, afterRevision: true);
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
            await FinalizeApprovalAsync(process, afterRevision: false);
        }
        else
        {
            process.Status = ApprovalProcessStatus.RevisionNeeded;

            await NotifyAsync(
                VndApprovalNotificationMessages.RevisionNeeded(process.Redaction!.Code, process.Vnd!.TitleRu),
                NotificationCategory.Approval, process.VndId, null, process.InitiatorUserId);
        }

        if (save) await _db.SaveChangesAsync();
    }

    private async Task CompleteRepeatPhaseAsync(VndApprovalProcess process, bool save = true)
    {
        process.Status = ApprovalProcessStatus.FinalHold;
        process.FinalHoldStartedAt = DateTime.UtcNow;

        foreach (var stage in process.Stages)
        {
            stage.FinalHoldDecision = ApprovalStageDecision.Pending;
            stage.FinalHoldComment = null;
            stage.FinalHoldDecidedAt = null;
        }

        var stageApproverIds = process.Stages.Select(s => s.ApproverUserId).ToArray();

        // --- Всем согласующим: документ ушёл на финальную выдержку
        await NotifyAsync(
            VndApprovalNotificationMessages.FinalHoldForApprovers(process.Redaction!.Code, process.Vnd!.TitleRu),
            NotificationCategory.Approval, process.VndId, null, stageApproverIds);

        // --- Инициатору: его редакция отправлена на финальную выдержку
        await NotifyAsync(
            VndApprovalNotificationMessages.SentToFinalHold(process.Redaction!.Code),
            NotificationCategory.Approval, process.VndId, null, process.InitiatorUserId);

        if (save) await _db.SaveChangesAsync();
    }

    /// <summary>Кто-то на финальной выдержке оставил замечание/отклонил - возвращаем
    /// процесс на доработку. Матрица разногласий предыдущего круга (если была) не трогается,
    /// инициатор сможет дополнить/почистить её строки заново на фронте.</summary>
    private async Task ReturnToRevisionFromFinalHoldAsync(VndApprovalProcess process)
    {
        process.Status = ApprovalProcessStatus.RevisionNeeded;

        await NotifyAsync(
            VndApprovalNotificationMessages.RevisionNeeded(process.Redaction!.Code, process.Vnd!.TitleRu),
            NotificationCategory.Approval, process.VndId, null, process.InitiatorUserId);
    }

    private async Task FinalizeApprovalAsync(VndApprovalProcess process, bool afterRevision)
    {
        process.Status = ApprovalProcessStatus.Approved;
        process.CompletedAt = DateTime.UtcNow;

        var redaction = process.Redaction!;
        var vnd = process.Vnd!;

        redaction.ApprovalStatus = RedactionApprovalStatus.Approved;

        // Согласование завершено, но документ ещё не публикуется автоматически -
        // CurrentRedactionId и RevisionChangedDate выставит VndActualizationService.PublishAsync
        // в момент явной публикации из статуса Consolidation.
        vnd.Status = VndStatus.Consolidation;

        var notice = afterRevision
            ? VndApprovalNotificationMessages.ApprovedAfterRevision(redaction.Code, vnd.TitleRu)
            : VndApprovalNotificationMessages.Approved(redaction.Code, vnd.TitleRu);

        // --- Инициатору (и ответственному за актуализацию, если это цикл актуализации):
        // документ перешёл в консолидацию
        var consolidationRecipients = new List<int> { process.InitiatorUserId };
        if (vnd.ActualizationResponsibleUserId.HasValue)
            consolidationRecipients.Add(vnd.ActualizationResponsibleUserId.Value);

        await NotifyAsync(
            notice, NotificationCategory.Approval, process.VndId, null, consolidationRecipients.ToArray());
    }

    private async Task<List<VndApprovalStage>> BuildAndValidateStagesAsync(List<ApprovalStageRequest> requestStages)
    {
        if (requestStages.Count < 3)
            throw new InvalidOperationException(
                "Маршрут должен содержать минимум 3 этапа: Юр. управление, Риск-менеджмент и Комплаенс");

        if (requestStages[0].Kind != ApprovalStageKind.Legal)
            throw new InvalidOperationException("Первый этап маршрута всегда — Юридическое управление");
        if (requestStages[1].Kind != ApprovalStageKind.RiskManagement)
            throw new InvalidOperationException("Второй этап маршрута всегда — Управление риск-менеджмента");
        if (requestStages[2].Kind != ApprovalStageKind.Compliance)
            throw new InvalidOperationException("Третий этап маршрута всегда — Управление комплаенс-контроля");

        for (var i = 3; i < requestStages.Count; i++)
        {
            if (requestStages[i].Kind != ApprovalStageKind.Custom
                && requestStages[i].Kind != ApprovalStageKind.Methodology)
                throw new InvalidOperationException(
                    "Этапы после фиксированных (Юр. управление, Риск-менеджмент, Комплаенс) " +
                    "должны иметь тип Custom или Методология");
        }

        var approverIds = requestStages.Select(s => s.ApproverUserId).ToList();
        if (approverIds.Distinct().Count() != approverIds.Count)
            throw new InvalidOperationException("Один пользователь не может занимать два этапа одновременно");

        var users = await _db.Users
            .Include(u => u.Roles)
            .Where(u => approverIds.Contains(u.Id))
            .ToListAsync();
        var missing = approverIds.Except(users.Select(u => u.Id)).ToList();
        if (missing.Count > 0)
            throw new KeyNotFoundException($"Пользователи с id={string.Join(", ", missing)} не найдены");

        var noApproverRight = users
            .Where(u => !u.Roles.SelectMany(r => r.PermissionCodes).Contains((int)PermissionCode.ActAsApprover))
            .Select(u => u.FullName)
            .ToList();
        if (noApproverRight.Count > 0)
            throw new InvalidOperationException(
                $"У пользователей нет права выступать в роли согласующего: {string.Join(", ", noApproverRight)}");

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
                   .Include(x => x.DisagreementMatrixRows)
                   .FirstOrDefaultAsync(x => x.RedactionId == lastRedaction.Id)
               ?? throw new KeyNotFoundException("Для последней редакции согласование не запускалось");
    }
    
    /// <summary>Причастен ли пользователь к документу - та же логика,
    /// что и в VndService.IsLinkedToVndAsync, но без проверки существующих
    /// процессов согласования (для Start процесса ещё нет).</summary>
    private async Task<bool> IsLinkedToVndAsync(VndDocument vnd, int currentUserId)
    {
        if (vnd.CreatedByUserId == currentUserId) return true;
        if (vnd.CuratorDeveloperId == currentUserId) return true;
        if (vnd.ActualizationResponsibleUserId == currentUserId) return true;

        return await _db.Entry(vnd)
            .Collection(x => x.ResponsibleExecutors)
            .Query()
            .AnyAsync(e => e.CuratorUserId == currentUserId);
    }

    private async Task<ApprovalProcessResponse> LoadResponseAsync(int processId)
    {
        var process = await _db.VndApprovalProcesses
            .Include(x => x.Stages).ThenInclude(s => s.OrgUnit)
            .Include(x => x.Stages).ThenInclude(s => s.ApproverUser)
            .Include(x => x.DisagreementMatrixRows)
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
            PrimaryDeadlineMinutes = process.PrimaryDeadlineMinutes,
            RepeatDeadlineMinutes = process.RepeatDeadlineMinutes,
            FinalHoldDeadlineMinutes = process.FinalHoldDeadlineMinutes,
            PrimaryStartedAt = process.PrimaryStartedAt,
            PrimaryDeadlineAt = process.PrimaryDeadlineAt,
            RepeatInitiatorComment = process.RepeatInitiatorComment,
            RepeatStartedAt = process.RepeatStartedAt,
            RepeatDeadlineAt = process.RepeatDeadlineAt,
            FinalHoldStartedAt = process.FinalHoldStartedAt,
            FinalHoldDeadlineAt = process.FinalHoldDeadlineAt,
            CompletedAt = process.CompletedAt,
            CreatedAt = process.CreatedAt,
            UpdatedAt = process.UpdatedAt,
            DisagreementMatrixRows = process.DisagreementMatrixRows
                .OrderBy(r => r.CreatedAt)
                .Select(ToDisagreementRowResponse)
                .ToList(),
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
                RepeatDecidedAt = s.RepeatDecidedAt,
                FinalHoldDecision = s.FinalHoldDecision.HasValue ? MapDecision(s.FinalHoldDecision.Value) : null,
                FinalHoldComment = s.FinalHoldComment,
                FinalHoldDecidedAt = s.FinalHoldDecidedAt
            }).ToList()
        };
    }

    private static DisagreementMatrixRowResponse ToDisagreementRowResponse(VndDisagreementMatrixRow row) => new()
    {
        Id = row.Id,
        DeveloperPosition = row.DeveloperPosition,
        OpponentPosition = row.OpponentPosition,
        DeveloperJustification = row.DeveloperJustification,
        CreatedAt = row.CreatedAt
    };

    /// <summary>
    /// Общий хелпер отправки уведомлений по событиям согласования.
    /// Принимает уже переведённый на 3 языка текст (см. VndApprovalNotificationMessages).
    /// Ошибка отправки не должна ронять сам процесс согласования - только логируется.
    /// </summary>
    private async Task NotifyAsync(
        NotificationText text, NotificationCategory category,
        int vndId, int? triggeredByUserId, params int[] recipientUserIds)
    {
        if (recipientUserIds.Length == 0) return;

        try
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = text.TitleRu,
                TitleEn = text.TitleEn,
                TitleKg = text.TitleKg,
                BodyRu = text.BodyRu,
                BodyEn = text.BodyEn,
                BodyKg = text.BodyKg,
                Category = category,
                Severity = text.Severity,
                EntityType = "Vnd",
                EntityId = vndId,
                Url = $"/base-vnd/{vndId}",
                UserIds = recipientUserIds.Distinct().ToList()
            }, triggeredByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Не удалось отправить уведомление по согласованию ВНД (vndId={VndId}, category={Category})",
                vndId, category);
        }
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