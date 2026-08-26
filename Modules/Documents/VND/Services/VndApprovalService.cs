using delosfera_server.Common.Services.Authorization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.ActivityLog.Models;
using delosfera_server.Modules.ActivityLog.Services;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Messages;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Users.Models;
using ActivityText = delosfera_server.Modules.ActivityLog.Models.ActivityText;

namespace delosfera_server.Modules.Documents.VND.Services;

public class VndApprovalService : IVndApprovalService
{
    // Верхняя граница норматива срока согласования — 90 дней. Должна совпадать с
    // MAX_DEADLINE_MINUTES на клиенте (src/constants/coordinationParams.ts).
    private const int MaxDeadlineMinutes = 90 * 24 * 60;

    // Максимальная длина комментария к резолюции согласующего и комментария инициатора
    // при повторной отправке. Должна совпадать с MAX_RESOLUTION_COMMENT_LENGTH на клиенте
    // (src/constants/coordinationParams.ts) — там ограничение только визуальное (maxLength
    // на textarea), реальную защиту от прямых запросов к API даёт именно эта проверка.
    private const int MaxResolutionCommentLength = 35000;

    // Максимальное число файлов, которые согласующий может приложить к своей резолюции за
    // один раз, и максимальный размер КАЖДОГО отдельного файла (не суммарно). Должны
    // совпадать с MAX_RESOLUTION_ATTACHMENTS и MAX_RESOLUTION_ATTACHMENT_SIZE_BYTES на
    // клиенте (src/constants/coordinationParams.ts).
    private const int MaxResolutionAttachments = 5;
    private const long MaxResolutionAttachmentSizeBytes = 50L * 1024 * 1024;

    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _fileService;
    private readonly INotificationService _notifications;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VndApprovalService> _logger;
    private readonly IActivityLogService _activityLog;

    public VndApprovalService(
        DelosferaDbContext db,
        IFileStorageService fileService,
        INotificationService notifications,
        ICurrentUserService currentUser,
        ILogger<VndApprovalService> logger,
        IActivityLogService activityLog)
    {
        _db = db;
        _fileService = fileService;
        _notifications = notifications;
        _currentUser = currentUser;
        _logger = logger;
        _activityLog = activityLog;
    }

    private bool IsChiefEditor() =>
        _currentUser.HasPermission(PermissionCode.CreateVndWithApproval)
        || _currentUser.HasPermission(PermissionCode.CreateVndWithoutApproval)
        || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval)
        || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);

    public async Task<ApprovalProcessResponse> StartAsync(int vndId, StartApprovalRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (!IsChiefEditor() && !await IsLinkedToVndAsync(vnd, currentUserId))
            throw new UnauthorizedAccessException(
                "Запустить согласование может только разработчик, куратор, ответственный исполнитель, " +
                "ответственный за актуализацию или главный редактор ВНД");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        var lastRedaction = await _db.VndRedactions
                                .Where(r => r.VndId == vndId)
                                .OrderByDescending(r => r.Number)
                                .FirstOrDefaultAsync()
                            ?? throw new InvalidOperationException("У ВНД ещё нет ни одной редакции");

        // Особый случай: заявленная "актуализация без изменений" с согласованием (см.
        // VndDocument.ActualizationPlannedNoChanges) — новая редакция не загружалась вообще,
        // на согласование уходит СУЩЕСТВУЮЩАЯ действующая редакция как есть (её ApprovalStatus
        // в этот момент Approved/NotRequired от прошлого цикла, не Draft). Если согласующие
        // всё же попросят доработку — ResubmitAfterRevisionAsync обновит файлы этой же редакции
        // на месте, как и в обычном цикле, никакой новой строки VndRedaction не создаётся.
        var isNoChangesReviewRound = vnd.Status == VndStatus.OnActualization && vnd.ActualizationPlannedNoChanges;

        if (lastRedaction.ApprovalStatus != RedactionApprovalStatus.Draft && !isNoChangesReviewRound)
            throw new InvalidOperationException(
                "На согласование можно отправить только редакцию в статусе черновика (ещё не отправленную)");

        var alreadyRunning = await _db.VndApprovalProcesses
            .AnyAsync(x => x.RedactionId == lastRedaction.Id && x.Status != ApprovalProcessStatus.Approved
                                                             && x.Status != ApprovalProcessStatus.Cancelled
                                                             && x.Status != ApprovalProcessStatus.Rejected);
        if (alreadyRunning)
            throw new InvalidOperationException("По этой редакции уже запущено согласование");

        if (request.PrimaryDeadlineMinutes <= 0 || request.RepeatDeadlineMinutes <= 0 ||
            request.FinalHoldDeadlineMinutes <= 0)
            throw new InvalidOperationException("Все три норматива должны быть больше нуля минут");

        // Верхняя граница нужна не только для здравого смысла, но и чтобы не уронить
        // расчёт дедлайна: PrimaryStartedAt.AddMinutes(...) кидает ArgumentOutOfRangeException,
        // если результат выходит за пределы DateTime, а слишком большое int-значение минут
        // (например, случайно введённое количество часов вместо минут) на это способно.
        if (request.PrimaryDeadlineMinutes > MaxDeadlineMinutes ||
            request.RepeatDeadlineMinutes > MaxDeadlineMinutes ||
            request.FinalHoldDeadlineMinutes > MaxDeadlineMinutes)
            throw new InvalidOperationException(
                $"Норматив срока не может превышать {MaxDeadlineMinutes / 60 / 24} дней");

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

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ProcessStarted, vndId, vnd.Code,
            currentUserId,
            new ActivityText(
                $"{actorName} запустил(а) согласование редакции {lastRedaction.Code} ВНД «{vnd.TitleRu}»",
                $"{actorName} started approval of revision {lastRedaction.Code} of VND \"{vnd.TitleRu}\"",
                $"{actorName} «{vnd.TitleRu}» ВНДисинин {lastRedaction.Code} редакциясын макулдашууну баштады"),
            $"/base-vnd/{vndId}");
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

        // См. комментарий в LoadProcessForVndAsync — сортировка нужна на случай, если одна и та же
        // редакция уже проходила согласование раньше (актуализация без изменений, повторный цикл).
        var process = await _db.VndApprovalProcesses
                          .Where(x => x.RedactionId == lastRedaction.Id)
                          .OrderByDescending(x => x.CreatedAt)
                          .FirstOrDefaultAsync()
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

        if (request.Comment is { Length: > MaxResolutionCommentLength })
            throw new InvalidOperationException(
                $"Комментарий не может превышать {MaxResolutionCommentLength} символов");

        if (request.Files is { Count: > MaxResolutionAttachments })
            throw new InvalidOperationException(
                $"К резолюции нельзя приложить больше {MaxResolutionAttachments} файлов");

        var oversizedFile = request.Files?.FirstOrDefault(f => f.Length > MaxResolutionAttachmentSizeBytes);
        if (oversizedFile is not null)
            throw new InvalidOperationException(
                $"Файл \"{oversizedFile.FileName}\" превышает максимальный размер " +
                $"{MaxResolutionAttachmentSizeBytes / 1024 / 1024} МБ на один файл");

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

                await AttachDecisionFilesAsync(stage, ApprovalStagePhase.Primary, request.Files, currentUserId);

                await _db.SaveChangesAsync();

                if (decision == ApprovalStageDecision.Rejected)
                {
                    // Отклонение - не то же самое, что "согласовано с замечаниями": оно не ждёт
                    // решения остальных, а сразу прекращает весь процесс (см. RejectApprovalAsync).
                    await RejectApprovalAsync(process, currentUserId, request.Comment);
                    await _db.SaveChangesAsync();
                }
                else if (process.Stages.All(s => s.PrimaryDecision != ApprovalStageDecision.Pending))
                {
                    await CompletePrimaryPhaseAsync(process);
                }
                break;

            case ApprovalProcessStatus.Repeated:
                if (!stage.ParticipatesInRepeat)
                    throw new InvalidOperationException("Этот согласующий не участвует в повторном согласовании");
                if (stage.RepeatDecision is not null && stage.RepeatDecision != ApprovalStageDecision.Pending)
                    throw new InvalidOperationException("Решение по повторному согласованию уже принято");

                stage.RepeatDecision = decision;
                stage.RepeatComment = request.Comment;
                stage.RepeatDecidedAt = DateTime.UtcNow;

                await AttachDecisionFilesAsync(stage, ApprovalStagePhase.Repeat, request.Files, currentUserId);

                await _db.SaveChangesAsync();

                if (decision == ApprovalStageDecision.Rejected)
                {
                    await RejectApprovalAsync(process, currentUserId, request.Comment);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    var repeatStages = process.Stages.Where(s => s.ParticipatesInRepeat).ToList();
                    if (repeatStages.All(s =>
                            s.RepeatDecision is not null && s.RepeatDecision != ApprovalStageDecision.Pending))
                        await CompleteRepeatPhaseAsync(process);
                }
                break;

            case ApprovalProcessStatus.FinalHold:
                if (stage.FinalHoldDecision is not null && stage.FinalHoldDecision != ApprovalStageDecision.Pending)
                    throw new InvalidOperationException("Решение по финальной выдержке уже принято");

                stage.FinalHoldDecision = decision;
                stage.FinalHoldComment = request.Comment;
                stage.FinalHoldDecidedAt = DateTime.UtcNow;

                await AttachDecisionFilesAsync(stage, ApprovalStagePhase.FinalHold, request.Files, currentUserId);

                await _db.SaveChangesAsync();

                if (decision == ApprovalStageDecision.Rejected)
                {
                    // В отличие от замечания на финальной выдержке (которое лишь возвращает на
                    // доработку в рамках того же процесса), отклонение прекращает его совсем.
                    await RejectApprovalAsync(process, currentUserId, request.Comment);
                    await _db.SaveChangesAsync();
                }
                else if (decision == ApprovalStageDecision.ApprovedWithComment)
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

        var logKind = decision switch
        {
            ApprovalStageDecision.Approved => ActivityEventKind.Approved,
            ApprovalStageDecision.ApprovedWithComment => ActivityEventKind.ApprovedWithComment,
            ApprovalStageDecision.Rejected => ActivityEventKind.Rejected,
            _ => ActivityEventKind.Other
        };

        _activityLog.Log(
            ActivityModules.Vnd, logKind, vndId, process.Vnd!.Code, currentUserId,
            decision == ApprovalStageDecision.Rejected
                ? new ActivityText(
                    $"{approverName} отклонил(а) редакцию {redactionCode} ВНД «{vndTitle}»",
                    $"{approverName} rejected revision {redactionCode} of VND \"{vndTitle}\"",
                    $"{approverName} «{vndTitle}» ВНДисинин {redactionCode} редакциясын четке какты")
                : new ActivityText(
                    $"{approverName} согласовал(а) редакцию {redactionCode} ВНД «{vndTitle}»",
                    $"{approverName} approved revision {redactionCode} of VND \"{vndTitle}\"",
                    $"{approverName} «{vndTitle}» ВНДисинин {redactionCode} редакциясын макулдады"),
            $"/base-vnd/{vndId}");
        await _db.SaveChangesAsync();

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

    /// <summary>
    /// Отзыв согласования инициатором (или главным редактором). Незавершённый процесс
    /// переводится в Cancelled, редакция и документ возвращаются в черновик/актуализацию,
    /// согласующим уходит уведомление, что задача снята.
    /// </summary>
    public async Task<ApprovalProcessResponse> CancelAsync(int vndId, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        // IsChiefEditor() тут не подходит - она проверяет права на СОЗДАНИЕ/актуализацию ВНД
        // (CreateVndWithApproval и т.п.), которыми на практике обладает почти любой автор ВНД,
        // а не только главный редактор. Поэтому отзыв "чужого" согласования - отдельное,
        // намеренно узкое право CancelAnyVndApproval.
        if (process.InitiatorUserId != currentUserId
            && !_currentUser.HasPermission(PermissionCode.CancelAnyVndApproval))
            throw new UnauthorizedAccessException(
                "Отозвать согласование может только инициатор или главный редактор");

        if (process.Status is ApprovalProcessStatus.Approved
            or ApprovalProcessStatus.Cancelled
            or ApprovalProcessStatus.Rejected)
            throw new InvalidOperationException("Согласование уже завершено — отозвать нельзя");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        process.Status = ApprovalProcessStatus.Cancelled;
        process.CompletedAt = DateTime.UtcNow;

        var redaction = process.Redaction!;
        var vnd = process.Vnd!;

        // Редакция снова становится черновиком (её можно править, переотправить или удалить).
        redaction.ApprovalStatus = RedactionApprovalStatus.Draft;

        // Документ: если это была первая редакция — возвращаем в черновик; если это цикл
        // актуализации существующего ВНД — возвращаем на актуализацию, а не в черновик.
        vnd.Status = redaction.Number <= 1 ? VndStatus.Draft : VndStatus.OnActualization;

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Other, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} отозвал(а) согласование редакции {redaction.Code} ВНД «{vnd.TitleRu}»",
                $"{actorName} withdrew approval of revision {redaction.Code} of VND \"{vnd.TitleRu}\"",
                $"{actorName} «{vnd.TitleRu}» ВНДисинин {redaction.Code} редакциясынын макулдашуусун артка алды"),
            $"/base-vnd/{vndId}");

        await _db.SaveChangesAsync();

        var approverIds = process.Stages
            .Select(s => s.ApproverUserId)
            .Where(id => id != currentUserId)
            .Distinct()
            .ToArray();

        await NotifyAsync(
            VndApprovalNotificationMessages.Cancelled(redaction.Code, vnd.TitleRu),
            NotificationCategory.Approval, vndId, currentUserId, approverIds);

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

        if (request.Comment is { Length: > MaxResolutionCommentLength })
            throw new InvalidOperationException(
                $"Комментарий не может превышать {MaxResolutionCommentLength} символов");

        var redaction = process.Redaction!;

        // ТИД обязателен на каждом круге доработки, если он был обязателен при первичной подаче
        // редакции (Number > 1 - значит документ актуализируется, а не создаётся впервые).
        var requiresTid = redaction.Number > 1;
        if (requiresTid && request.Tid is null)
            throw new InvalidOperationException(
                "При актуализации ВНД необходимо приложить обновлённый файл ТИД вместе с исправленной редакцией");

        // Момент замены — общий для всех документов, заменённых в рамках одной отправки,
        // чтобы метки "Обновлено, дата" на фронте показывали одно и то же время.
        var resubmittedAt = DateTime.UtcNow;

        if (request.DocRu is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocRu, currentUserId);
            redaction.DocFileRuId = saved.Id;
            redaction.DocRuUpdatedAt = resubmittedAt;
        }

        if (request.DocKg is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocKg, currentUserId);
            redaction.DocFileKgId = saved.Id;
            redaction.DocKgUpdatedAt = resubmittedAt;
        }
        else if (request.RemoveDocKg)
        {
            // Явное удаление документа на кыргызском без замены (см. ResubmitAfterRevisionRequest.RemoveDocKg).
            redaction.DocFileKgId = null;
            redaction.DocKgUpdatedAt = null;
        }

        if (request.DocEn is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocEn, currentUserId);
            redaction.DocFileEnId = saved.Id;
            redaction.DocEnUpdatedAt = resubmittedAt;
        }
        else if (request.RemoveDocEn)
        {
            redaction.DocFileEnId = null;
            redaction.DocEnUpdatedAt = null;
        }

        if (request.Tid is not null)
        {
            var saved = await _fileService.SaveAsync(request.Tid, currentUserId);
            redaction.TidFileId = saved.Id;
        }

        // Новые вложения к редакции - добавляем в уже отслеживаемую EF навигацию, FK на редакцию
        // проставится автоматически при SaveChangesAsync (не требует предварительной загрузки
        // коллекции, см. AddRedactionAsync в VndService для того же паттерна на создании).
        foreach (var file in request.NewAttachments ?? [])
        {
            var saved = await _fileService.SaveAsync(file, currentUserId);
            redaction.Attachments.Add(new VndRedactionAttachment { FileAttachmentId = saved.Id });
        }

        if (request.RemovedAttachmentFileIds is { Count: > 0 })
        {
            var toRemove = await _db.Set<VndRedactionAttachment>()
                .Where(a => a.VndRedactionId == redaction.Id &&
                            request.RemovedAttachmentFileIds.Contains(a.FileAttachmentId))
                .ToListAsync();
            _db.Set<VndRedactionAttachment>().RemoveRange(toRemove);
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

            // Инициатор мог быть согласующим на одном из этапов - на повторном согласовании
            // его решение тоже проставляется автоматически, иначе оно "висит" до просрочки.
            AutoApproveInitiatorStages(process, ApprovalStagePhase.Repeat);

            await _db.SaveChangesAsync();

            var repeatApproverIds = process.Stages
                .Where(s => s.ParticipatesInRepeat)
                .Select(s => s.ApproverUserId)
                .ToArray();

            await NotifyAsync(
                VndApprovalNotificationMessages.TaskRepeatApproval(redaction.Code, process.Vnd!.TitleRu),
                NotificationCategory.Approval, vndId, currentUserId, repeatApproverIds);

            var repeatStages = process.Stages.Where(s => s.ParticipatesInRepeat).ToList();
            if (repeatStages.Count > 0 && repeatStages.All(s =>
                    s.RepeatDecision is not null && s.RepeatDecision != ApprovalStageDecision.Pending))
            {
                await CompleteRepeatPhaseAsync(process);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            // Составлена матрица разногласий - повторное согласование пропускаем,
            // сразу идём на финальную выдержку (решение по-прежнему требуется только от тех,
            // кто ещё не давал чистого согласования этой редакции - см. ResetFinalHoldDecisions)
            process.Status = ApprovalProcessStatus.FinalHold;
            ResetFinalHoldDecisions(process);
            process.FinalHoldStartedAt = DateTime.UtcNow;

            // См. комментарий выше - тот же самообход для финальной выдержки.
            AutoApproveInitiatorStages(process, ApprovalStagePhase.FinalHold);

            await _db.SaveChangesAsync();

            var stageApproverIds = process.Stages.Select(s => s.ApproverUserId).ToArray();

            await NotifyAsync(
                VndApprovalNotificationMessages.FinalHoldForApprovers(redaction.Code, process.Vnd!.TitleRu),
                NotificationCategory.Approval, vndId, currentUserId, stageApproverIds);

            await NotifyAsync(
                VndApprovalNotificationMessages.SentToFinalHold(redaction.Code),
                NotificationCategory.Approval, vndId, currentUserId, currentUserId);

            if (process.Stages.All(s =>
                    s.FinalHoldDecision is not null && s.FinalHoldDecision != ApprovalStageDecision.Pending))
            {
                await FinalizeApprovalAsync(process, afterRevision: true);
                await _db.SaveChangesAsync();
            }
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

                _activityLog.Log(
                    ActivityModules.Vnd, ActivityEventKind.AutoApprovedTimeout,
                    process.VndId, process.Vnd!.Code, null,
                    new ActivityText(
                        $"Просрочен срок согласования редакции {process.Redaction!.Code} ВНД «{process.Vnd!.TitleRu}» — применён автоакцепт",
                        $"Approval deadline missed for revision {process.Redaction!.Code} of VND \"{process.Vnd!.TitleRu}\" — auto-approved",
                        $"«{process.Vnd!.TitleRu}» ВНДисинин {process.Redaction!.Code} редакциясын макулдашуу мөөнөтү өттү — автоматтык түрдө макулдашылды"),
                    $"/base-vnd/{process.VndId}");
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

                _activityLog.Log(
                    ActivityModules.Vnd, ActivityEventKind.AutoApprovedTimeout,
                    process.VndId, process.Vnd!.Code, null,
                    new ActivityText(
                        $"Просрочен срок повторного согласования редакции {process.Redaction!.Code} ВНД «{process.Vnd!.TitleRu}» — применён автоакцепт",
                        $"Repeated approval deadline missed for revision {process.Redaction!.Code} of VND \"{process.Vnd!.TitleRu}\" — auto-approved",
                        $"«{process.Vnd!.TitleRu}» ВНДисинин {process.Redaction!.Code} редакциясын кайра макулдашуу мөөнөтү өттү — автоматтык түрдө макулдашылды"),
                    $"/base-vnd/{process.VndId}");
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

                _activityLog.Log(
                    ActivityModules.Vnd, ActivityEventKind.AutoApprovedTimeout,
                    process.VndId, process.Vnd!.Code, null,
                    new ActivityText(
                        $"Просрочен срок финальной выдержки редакции {process.Redaction!.Code} ВНД «{process.Vnd!.TitleRu}» — применён автоакцепт",
                        $"Final hold deadline missed for revision {process.Redaction!.Code} of VND \"{process.Vnd!.TitleRu}\" — auto-approved",
                        $"«{process.Vnd!.TitleRu}» ВНДисинин {process.Redaction!.Code} редакциясынын акыркы кармоо мөөнөтү өттү — автоматтык түрдө макулдашылды"),
                    $"/base-vnd/{process.VndId}");
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

        ResetFinalHoldDecisions(process);

        // Инициатор мог быть согласующим на одном из этапов - на финальной выдержке
        // его решение тоже проставляется автоматически, иначе оно "висит" до просрочки.
        AutoApproveInitiatorStages(process, ApprovalStagePhase.FinalHold);

        var stageApproverIds = process.Stages.Select(s => s.ApproverUserId).ToArray();

        // --- Всем согласующим: документ ушёл на финальную выдержку
        await NotifyAsync(
            VndApprovalNotificationMessages.FinalHoldForApprovers(process.Redaction!.Code, process.Vnd!.TitleRu),
            NotificationCategory.Approval, process.VndId, null, stageApproverIds);

        // --- Инициатору: его редакция отправлена на финальную выдержку
        await NotifyAsync(
            VndApprovalNotificationMessages.SentToFinalHold(process.Redaction!.Code),
            NotificationCategory.Approval, process.VndId, null, process.InitiatorUserId);

        // Если самообход инициатора уже закрыл все решения финальной выдержки (например,
        // маршрут состоит из одного этапа, и на нём согласующий - сам инициатор) - сразу
        // завершаем согласование, не дожидаясь дедлайна.
        if (process.Stages.All(s =>
                s.FinalHoldDecision is not null && s.FinalHoldDecision != ApprovalStageDecision.Pending))
        {
            await FinalizeApprovalAsync(process, afterRevision: true);
        }

        if (save) await _db.SaveChangesAsync();
    }

    /// <summary>Кто-то на финальной выдержке оставил замечание (не отклонение - оно прекращает
    /// процесс совсем, см. RejectApprovalAsync) - возвращаем процесс на доработку. Матрица
    /// разногласий предыдущего круга (если была) не трогается, инициатор сможет дополнить/
    /// почистить её строки заново на фронте.</summary>
    private async Task ReturnToRevisionFromFinalHoldAsync(VndApprovalProcess process)
    {
        process.Status = ApprovalProcessStatus.RevisionNeeded;

        await NotifyAsync(
            VndApprovalNotificationMessages.RevisionNeeded(process.Redaction!.Code, process.Vnd!.TitleRu),
            NotificationCategory.Approval, process.VndId, null, process.InitiatorUserId);
    }

    /// <summary>Отклонение редакции одним из согласующих - жёсткое немедленное завершение
    /// процесса согласования, в отличие от "согласовано с замечаниями" (которое лишь возвращает
    /// на доработку в рамках того же процесса и ждёт решения остальных). Редакция и документ
    /// возвращаются в черновик/актуализацию - как при отзыве согласования (CancelAsync), только
    /// это происходит автоматически по решению согласующего, без отдельного действия инициатора
    /// или главного редактора. Всем остальным согласующим, чьё решение на активной на момент
    /// отклонения фазе ещё не принято, снимается задача - решать больше не по чему.</summary>
    private async Task RejectApprovalAsync(VndApprovalProcess process, int rejectedByUserId, string? comment)
    {
        var phaseAtRejection = process.Status;

        process.Status = ApprovalProcessStatus.Rejected;
        process.CompletedAt = DateTime.UtcNow;

        var redaction = process.Redaction!;
        var vnd = process.Vnd!;

        redaction.ApprovalStatus = RedactionApprovalStatus.Draft;
        vnd.Status = redaction.Number <= 1 ? VndStatus.Draft : VndStatus.OnActualization;

        var rejecter = await _db.Users.FindAsync(rejectedByUserId);
        var rejecterName = rejecter?.FullName ?? "—";

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Rejected, process.VndId, vnd.Code, rejectedByUserId,
            new ActivityText(
                $"{rejecterName} отклонил(а) редакцию {redaction.Code} ВНД «{vnd.TitleRu}» — согласование прекращено",
                $"{rejecterName} rejected revision {redaction.Code} of VND \"{vnd.TitleRu}\" — approval process stopped",
                $"{rejecterName} «{vnd.TitleRu}» ВНДисинин {redaction.Code} редакциясын четке какты — макулдашуу токтотулду"),
            $"/base-vnd/{process.VndId}");

        bool IsPendingOnPhase(VndApprovalStage s) => phaseAtRejection switch
        {
            ApprovalProcessStatus.Primary => s.PrimaryDecision == ApprovalStageDecision.Pending,
            ApprovalProcessStatus.Repeated => s.ParticipatesInRepeat &&
                (s.RepeatDecision is null || s.RepeatDecision == ApprovalStageDecision.Pending),
            ApprovalProcessStatus.FinalHold =>
                s.FinalHoldDecision is null || s.FinalHoldDecision == ApprovalStageDecision.Pending,
            _ => false
        };

        var pendingApproverIds = process.Stages
            .Where(s => s.ApproverUserId != rejectedByUserId && IsPendingOnPhase(s))
            .Select(s => s.ApproverUserId)
            .Distinct()
            .ToArray();

        if (pendingApproverIds.Length > 0)
            await NotifyAsync(
                VndApprovalNotificationMessages.ProcessRejectedTaskCancelled(
                    rejecterName, redaction.Code, vnd.TitleRu, comment),
                NotificationCategory.Approval, process.VndId, rejectedByUserId, pendingApproverIds);
    }

    private async Task FinalizeApprovalAsync(VndApprovalProcess process, bool afterRevision)
    {
        process.Status = ApprovalProcessStatus.Approved;
        process.CompletedAt = DateTime.UtcNow;

        var redaction = process.Redaction!;
        var vnd = process.Vnd!;

        redaction.ApprovalStatus = RedactionApprovalStatus.Approved;

        // Редакция стала согласованной - файлы, приложенные согласующими к своим резолюциям,
        // больше не нужны и удаляются, чтобы не копить их в БД/хранилище. Текст самих резолюций
        // (PrimaryComment/RepeatComment/FinalHoldComment) остаётся как есть.
        await CleanupStageAttachmentsAsync(process);

        // Согласование завершено, но документ ещё не публикуется автоматически -
        // CurrentRedactionId и RevisionChangedDate выставит VndActualizationService.PublishAsync
        // в момент явной публикации из статуса Consolidation.
        vnd.Status = VndStatus.Consolidation;

        // Если это часть цикла актуализации - фиксируем момент входа в консолидацию в открытой
        // записи истории (фильтр "Только связанные со мной" → "я консолидирую"/"я когда-то
        // консолидировал"). Обычное согласование вне актуализации открытой записи не имеет.
        if (vnd.ActualizationResponsibleUserId.HasValue)
        {
            var openRecord = await _db.Set<VndActualizationRecord>()
                .Where(r => r.VndId == process.VndId && r.PublishedAt == null)
                .OrderByDescending(r => r.StartedAt)
                .FirstOrDefaultAsync();

            if (openRecord is not null && openRecord.ConsolidationStartedAt is null)
                openRecord.ConsolidationStartedAt = DateTime.UtcNow;
        }

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Finalized, process.VndId, vnd.Code, null,
            new ActivityText(
                $"Редакция {redaction.Code} ВНД «{vnd.TitleRu}» согласована, ВНД переведён в статус «Консолидация»",
                $"Revision {redaction.Code} of VND \"{vnd.TitleRu}\" has been approved, VND moved to \"Consolidation\" status",
                $"«{vnd.TitleRu}» ВНДисинин {redaction.Code} редакциясы макулдашылды, ВНД «Консолидация» абалына өттү"),
            $"/base-vnd/{process.VndId}");

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

    /// <summary>Если инициатор согласования сам числится согласующим на одном из этапов, его
    /// решение на этой фазе проставляется автоматически - как и на первичном этапе при старте
    /// процесса (см. StartAsync). Без этого решение того же человека "зависает" в Pending на
    /// повторном согласовании/финальной выдержке до истечения дедлайна: сбросы в Pending при
    /// старте фазы (ResubmitAfterRevisionAsync, CompleteRepeatPhaseAsync) применяются одинаково
    /// ко всем этапам, включая тот, где согласующий - сам инициатор.
    /// Вызывать сразу после сброса решений фазы в Pending, до SaveChangesAsync; после вызова
    /// стоит проверить, не оказалась ли фаза уже полностью решена (см. вызывающий код).</summary>
    private static void AutoApproveInitiatorStages(VndApprovalProcess process, ApprovalStagePhase phase)
    {
        const string comment = "Согласовано автоматически — инициатор является согласующим на этом этапе";
        var now = DateTime.UtcNow;

        IEnumerable<VndApprovalStage> stages = phase switch
        {
            ApprovalStagePhase.Repeat => process.Stages.Where(s =>
                s.ApproverUserId == process.InitiatorUserId
                && s.ParticipatesInRepeat
                && s.RepeatDecision == ApprovalStageDecision.Pending),
            ApprovalStagePhase.FinalHold => process.Stages.Where(s =>
                s.ApproverUserId == process.InitiatorUserId
                && s.FinalHoldDecision == ApprovalStageDecision.Pending),
            _ => Enumerable.Empty<VndApprovalStage>()
        };

        foreach (var stage in stages)
        {
            if (phase == ApprovalStagePhase.Repeat)
            {
                stage.RepeatDecision = ApprovalStageDecision.Approved;
                stage.RepeatComment = comment;
                stage.RepeatDecidedAt = now;
            }
            else
            {
                stage.FinalHoldDecision = ApprovalStageDecision.Approved;
                stage.FinalHoldComment = comment;
                stage.FinalHoldDecidedAt = now;
            }
        }
    }

    /// <summary>Определяет, какое решение по этапу считается "актуальным" на момент входа в
    /// финальную выдержку — самое позднее из уже принятых (FinalHold нового круга ещё не
    /// проставлен на момент вызова, поэтому фактически это FinalHold ПРЕДЫДУЩЕГО круга, если
    /// он был, иначе Repeat, иначе Primary). Использовать ДО сброса FinalHoldDecision.</summary>
    private static ApprovalStageDecision? LatestDecisionBeforeFinalHold(VndApprovalStage stage) =>
        stage.FinalHoldDecision ?? stage.RepeatDecision ?? stage.PrimaryDecision;

    /// <summary>Готовит решения этапов к (пере)входу в финальную выдержку. Если согласующий уже
    /// дал по этой редакции чистое согласование без замечаний (не участвовал в повторном
    /// согласовании — т.е. его первичное решение было Approved/автоакцепт по таймауту, либо он
    /// уже чисто согласовал на предыдущем круге финальной выдержки) — его решение проставляется
    /// автоматически, и жать "Согласовать" ещё раз ему не нужно. Формального решения снова ждём
    /// только от тех, кто на этой редакции ранее оставлял замечания или отклонял её.
    /// Вызывать сразу после назначения process.Status = FinalHold, до SaveChangesAsync.</summary>
    private static void ResetFinalHoldDecisions(VndApprovalProcess process)
    {
        const string comment = "Согласовано автоматически — вы уже согласовали эту редакцию без замечаний ранее";
        var now = DateTime.UtcNow;

        foreach (var stage in process.Stages)
        {
            var latest = LatestDecisionBeforeFinalHold(stage);
            var wasClean = latest is ApprovalStageDecision.Approved or ApprovalStageDecision.AutoApprovedByTimeout;

            if (wasClean)
            {
                stage.FinalHoldDecision = ApprovalStageDecision.Approved;
                stage.FinalHoldComment = comment;
                stage.FinalHoldDecidedAt = now;
            }
            else
            {
                stage.FinalHoldDecision = ApprovalStageDecision.Pending;
                stage.FinalHoldComment = null;
                stage.FinalHoldDecidedAt = null;
            }
        }
    }

    /// <summary>Сохраняет файлы, приложенные согласующим к резолюции конкретной фазы, и
    /// связывает их с этапом. Вызывается из DecideAsync до SaveChangesAsync — сами
    /// вложения переживают до тех пор, пока редакция не станет согласованной
    /// (см. <see cref="CleanupStageAttachmentsAsync"/>).</summary>
    private async Task AttachDecisionFilesAsync(
        VndApprovalStage stage, ApprovalStagePhase phase, List<IFormFile>? files, int userId)
    {
        if (files is null || files.Count == 0) return;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            var stored = await _fileService.SaveAsync(file, userId);

            _db.Set<VndApprovalStageAttachment>().Add(new VndApprovalStageAttachment
            {
                VndApprovalStageId = stage.Id,
                Phase = phase,
                FileAttachmentId = stored.Id,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>Удаляет все файлы, приложенные согласующими к резолюциям этого процесса
    /// (по всем этапам и фазам), когда редакция становится согласованной — чтобы не копить
    /// файлы в БД и в хранилище. Текст резолюций (Primary/Repeat/FinalHoldComment) не трогается.</summary>
    private async Task CleanupStageAttachmentsAsync(VndApprovalProcess process)
    {
        var stageIds = process.Stages.Select(s => s.Id).ToList();
        if (stageIds.Count == 0) return;

        var attachments = await _db.Set<VndApprovalStageAttachment>()
            .Where(a => stageIds.Contains(a.VndApprovalStageId))
            .ToListAsync();

        if (attachments.Count == 0) return;

        foreach (var attachment in attachments)
        {
            try
            {
                await _fileService.DeleteAsync(attachment.FileAttachmentId);
            }
            catch (Exception ex)
            {
                // Сбой удаления файла из хранилища не должен срывать завершение согласования -
                // запись о вложении всё равно будет убрана ниже, а "осиротевший" файл в бакете
                // не критичен и может быть подчищен отдельно.
                _logger.LogWarning(ex,
                    "Не удалось удалить файл {FileId} вложения резолюции при завершении согласования процесса {ProcessId}",
                    attachment.FileAttachmentId, process.Id);
            }
        }

        _db.Set<VndApprovalStageAttachment>().RemoveRange(attachments);
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
            throw new InvalidOperationException("Один пользователь не может быть согласующим два раза одновременно");

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

        // OrderByDescending(CreatedAt) — обычно у редакции ровно один процесс согласования за всю
        // жизнь, но при "актуализации без изменений" (см. VndDocument.ActualizationPlannedNoChanges)
        // одна и та же действующая редакция может проходить согласование повторно в разных циклах
        // без создания новой строки VndRedaction — без сортировки здесь можно было бы случайно
        // получить старый уже завершённый процесс вместо актуального.
        return await _db.VndApprovalProcesses
                   .Include(x => x.Stages).ThenInclude(s => s.Attachments)
                   .Include(x => x.Redaction)
                   .Include(x => x.Vnd)
                   .Include(x => x.DisagreementMatrixRows)
                   .Where(x => x.RedactionId == lastRedaction.Id)
                   .OrderByDescending(x => x.CreatedAt)
                   .FirstOrDefaultAsync()
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
            .Include(x => x.Stages).ThenInclude(s => s.Attachments).ThenInclude(a => a.FileAttachment)
            .Include(x => x.DisagreementMatrixRows)
            .FirstAsync(x => x.Id == processId);

        var initiator = await _db.Users
            .Include(u => u.Position)
            .FirstOrDefaultAsync(u => u.Id == process.InitiatorUserId);

        return new ApprovalProcessResponse
        {
            Id = process.Id,
            VndId = process.VndId,
            RedactionId = process.RedactionId,
            InitiatorUserId = process.InitiatorUserId,
            InitiatorName = initiator?.FullName ?? "",
            InitiatorPosition = initiator?.Position?.TitleRu,
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
                PrimaryAttachments = ToAttachmentResponses(s.Attachments, ApprovalStagePhase.Primary),
                ParticipatesInRepeat = s.ParticipatesInRepeat,
                RepeatDecision = s.RepeatDecision.HasValue ? MapDecision(s.RepeatDecision.Value) : null,
                RepeatComment = s.RepeatComment,
                RepeatDecidedAt = s.RepeatDecidedAt,
                RepeatAttachments = ToAttachmentResponses(s.Attachments, ApprovalStagePhase.Repeat),
                FinalHoldDecision = s.FinalHoldDecision.HasValue ? MapDecision(s.FinalHoldDecision.Value) : null,
                FinalHoldComment = s.FinalHoldComment,
                FinalHoldDecidedAt = s.FinalHoldDecidedAt,
                FinalHoldAttachments = ToAttachmentResponses(s.Attachments, ApprovalStagePhase.FinalHold)
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

    /// <summary>Вложения этапа для конкретной фазы решения (первичной/повторной/финальной).
    /// Пусто, если согласование уже завершилось согласованием редакции — вложения к этому моменту
    /// уже удалены, остаётся только текст резолюции.</summary>
    private static List<ApprovalStageAttachmentResponse> ToAttachmentResponses(
        IEnumerable<VndApprovalStageAttachment> attachments, ApprovalStagePhase phase) =>
        attachments
            .Where(a => a.Phase == phase)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new ApprovalStageAttachmentResponse
            {
                Id = a.Id,
                FileId = a.FileAttachmentId,
                FileName = a.FileAttachment?.OriginalFileName ?? "",
                SizeBytes = a.FileAttachment?.SizeBytes ?? 0
            })
            .ToList();

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
        ApprovalProcessStatus.Rejected => "rejected",
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