using System.Text.Json;
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
    private const long MaxResolutionAttachmentSizeBytes = 100L * 1024 * 1024;

    // Цитаты из текста редакции, на которые согласующий сослался в резолюции (см.
    // "+ Сослаться на текст редакции" на клиенте). Лимит по количеству — защита от абьюза
    // API напрямую (в обычном UI-сценарии их будет единицы); MaxQuoteTextLength — с запасом
    // больше клиентского MAX_QUOTE_SOURCE_LENGTH (600, см. VndApproverResolutionPanel.tsx),
    // текст длиннее просто обрезаем, а не отклоняем весь запрос.
    private const int MaxQuotesPerDecision = 50;
    private const int MaxQuoteTextLength = 1000;

    /// <summary>Пояснение к решению по фазе, которую этап "пропустил", потому что был добавлен
    /// главным редактором (AddApproverAsync/ReplaceApproverAsync) уже после её начала. По нему же
    /// ResetFinalHoldDecisionsAsync отличает такой этап от "настоящего" чистого согласования.</summary>
    private const string SkippedPhaseComment =
        "Добавлен главным редактором после начала этой фазы согласования — не участвовал в ней";

    private const string InitiatorAutoApprovedComment =
        "Согласовано автоматически — инициатор является согласующим на этом этапе";

    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _fileService;
    private readonly INotificationService _notifications;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<VndApprovalService> _logger;
    private readonly IActivityLogService _activityLog;
    private readonly IApprovalSheetGenerator _approvalSheetGenerator;
    private readonly IFixedApprovalUnitResolver _fixedUnits;

    public VndApprovalService(
        DelosferaDbContext db,
        IFileStorageService fileService,
        INotificationService notifications,
        ICurrentUserService currentUser,
        ILogger<VndApprovalService> logger,
        IActivityLogService activityLog,
        IApprovalSheetGenerator approvalSheetGenerator,
        IFixedApprovalUnitResolver fixedUnits)
    {
        _db = db;
        _fileService = fileService;
        _notifications = notifications;
        _currentUser = currentUser;
        _logger = logger;
        _activityLog = activityLog;
        _approvalSheetGenerator = approvalSheetGenerator;
        _fixedUnits = fixedUnits;
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

        // Архивация необратима (см. VndService.CancelAsync) - у архивированного документа может
        // остаться редакция в статусе "черновик" (например, она была не отправлена на согласование
        // на момент архивации), но отправить её на согласование, "вытащив" ВНД тем самым обратно
        // из архива, всё равно нельзя.
        if (vnd.Status == VndStatus.Archived)
            throw new InvalidOperationException(
                "ВНД архивирован — архивация необратима, отправить на согласование нельзя");

        if (!IsChiefEditor() && !await IsLinkedToVndAsync(vnd, currentUserId))
            throw new UnauthorizedAccessException(
                "Запустить согласование может только разработчик, куратор, ответственный исполнитель, " +
                "ответственный за актуализацию или главный редактор ВНД");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        // Кто станет инициатором согласования. По умолчанию - сам действующий пользователь, как
        // было раньше. Но если запускает НЕ автор черновика (обычно - главный редактор запускает
        // согласование чужого черновика, см. IsChiefEditor() выше), это должен быть явный выбор,
        // а не всегда молчаливо currentUserId: настоящий автор черновика иначе не мог стать
        // инициатором своей же работы, даже когда фактически именно он её написал - согласование
        // просто нажатием чужой кнопки "приписывалось" не ему. См. VndSelectApproverModal/
        // VndStartApprovalModal на фронте - там же теперь и уведомление автору (SentToApproval
        // ниже, получатель расширен на CreatedByUserId).
        // Выбор инициатора имеет смысл только для настоящего главного редактора - у обычного
        // редактора (разработчика/куратора/ответственного исполнителя/ответственного за
        // актуализацию, см. IsLinkedToVndAsync выше) такого выбора быть не должно: он всегда сам
        // инициатор, когда отправляет, даже если формально не является автором самого черновика
        // (например, действует как ответственный за актуализацию, назначенный не тем, кто когда-то
        // создавал ВНД). Раньше здесь проверялось только draftOwnerId != currentUserId — из-за
        // этого обычный редактор, отправляющий чужой (по CreatedByUserId) документ, тоже видел
        // выбор "Кто будет указан инициатором согласования?", хотя мог быть только собой.
        //
        // IsChiefEditor() тут не подходит (тот же принцип, что и у CancelAnyVndApproval/
        // ConsolidateAnyVnd/EditAnyVndApprovalRoute выше по файлу и в PermissionCode.cs): её
        // широкий набор (CreateVndWith(out)Approval/ActualizeAnyVndWith(out)Approval) есть
        // практически у любого автора ВНД, включая обычных редакторов - то и подтвердилось на
        // практике: обычный редактор с одним лишь CreateVndWithApproval проходил эту проверку и
        // видел выбор инициатора. Нужно именно узкое право EditAnyVndApprovalRoute ("роль
        // главного редактора" - редактирование маршрута согласования), которым обычные редакторы
        // не наделяются.
        // Кто фактически подготовил редакцию/ТИД, отправляемые на согласование именно сейчас: если
        // сейчас идёт цикл актуализации, это её ответственный (vnd.ActualizationResponsibleUserId -
        // тот, кто нажал "Взять в актуализацию" и всё загрузил), а НЕ тот, кто когда-то создал сам
        // документ (vnd.CreatedByUserId) - иначе, когда согласование запускает главный редактор,
        // который к тому же и есть первоначальный создатель ВНД (а актуализацию по факту вёл кто-то
        // другой), draftOwnerId совпадал бы с currentUserId и выбор инициатора не предлагался бы
        // вовсе, хотя настоящий автор ЭТОЙ редакции - не он (реальный сценарий: Бермет создала ВНД,
        // Айбек взял её в актуализацию и всё загрузил, Бермет запускает согласование - до этой
        // правки панель выбора инициатора у неё не появлялась). Тот же принцип уже применён у
        // defaultResponsibleUserId/canSelectResponsible для поля "Разработчик" в VndUploadTidModal.
        var draftOwnerId = vnd.ActualizationResponsibleUserId ?? vnd.CreatedByUserId;
        var actingOnSomeoneElsesDraft =
            _currentUser.HasPermission(PermissionCode.EditAnyVndApprovalRoute)
            && draftOwnerId.HasValue && draftOwnerId.Value != currentUserId;

        if (!actingOnSomeoneElsesDraft && request.InitiatorUserId.HasValue &&
            request.InitiatorUserId.Value != currentUserId)
            throw new InvalidOperationException(
                "Выбор инициатора согласования доступен только при запуске согласования чужого черновика");

        if (actingOnSomeoneElsesDraft && request.InitiatorUserId.HasValue &&
            request.InitiatorUserId.Value != currentUserId && request.InitiatorUserId.Value != draftOwnerId!.Value)
            throw new InvalidOperationException("Инициатором можно указать только себя или автора черновика");

        var initiatorUserId = actingOnSomeoneElsesDraft && request.InitiatorUserId == draftOwnerId
            ? draftOwnerId!.Value
            : currentUserId;

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

        // Актуализационная редакция (Number > 1) не может уйти на согласование без ТИД — раньше это
        // проверялось при самой загрузке (AddRedactionAsync), теперь ТИД прикладывается отдельным
        // шагом позже (кнопка "Сформировать или загрузить ТИД"), поэтому проверка переехала сюда и в
        // VndService.PublishRedactionWithoutApprovalAsync. В цикле "без изменений" (isNoChangesReviewRound)
        // ТИД уже должен быть проставлен с прошлого цикла, так что проверка там безопасный no-op.
        if (lastRedaction.Number > 1 && lastRedaction.TidFileId is null)
            throw new InvalidOperationException(
                "Прежде чем отправить редакцию на согласование, приложите файл ТИД " +
                "(Таблица изменений и дополнений) — кнопка «Сформировать или загрузить ТИД»");

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

        // Себя можно указать согласующим только на обязательном (фиксированном) этапе из
        // справочника - принадлежность инициатора нужному подразделению всё равно проверяется
        // ниже в BuildAndValidateStagesAsync. На дополнительных (Custom) этапах, которые
        // инициатор сам добавил вручную (CoordinationStageId == null), себя указывать нельзя.
        if (request.Stages.Any(s => s.CoordinationStageId is null && s.ApproverUserId == currentUserId))
            throw new InvalidOperationException(
                "Вы не можете быть согласующим на дополнительном этапе, который сами добавили");

        var stages = await BuildAndValidateStagesAsync(request.Stages);

        // Этапы, где согласующий - сам инициатор (фиксированный этап его же подразделения),
        // считаем согласованными автоматически, без ожидания решения. Важно: это именно
        // ИНИЦИАТОР, а не просто "тот, кто нажал кнопку" (currentUserId) - раньше проверялось
        // только currentUserId == ApproverUserId, и когда главный редактор запускал
        // согласование чужого черновика, оставляя инициатором автора черновика (см.
        // initiatorUserId выше - actingOnSomeoneElsesDraft && request.InitiatorUserId ==
        // draftOwnerId), а сам при этом тоже оказывался закреплённым участником какого-то
        // фиксированного этапа, ему ошибочно ставилось автосогласование - хотя фактическим
        // инициатором в этом запуске является не он, а автор черновика. Автосогласование
        // должно применяться только когда currentUserId и есть инициатор: он либо отправляет
        // на согласование свою же редакцию, либо явно выбрал "Стать инициатором согласования"
        // для чужого черновика (initiatorUserId == currentUserId в обоих случаях).
        var now = DateTime.UtcNow;
        if (initiatorUserId == currentUserId)
        {
            foreach (var selfStage in stages.Where(s => s.ApproverUserId == currentUserId))
            {
                selfStage.PrimaryDecision = ApprovalStageDecision.Approved;
                selfStage.PrimaryComment = InitiatorAutoApprovedComment;
                selfStage.PrimaryDecidedAt = now;
            }
        }

        var process = new VndApprovalProcess
        {
            VndId = vndId,
            RedactionId = lastRedaction.Id,
            InitiatorUserId = initiatorUserId,
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

        // Инициатором указан НЕ тот, кто фактически запустил согласование (главный редактор
        // выбрал на VndStartApprovalModal вариант "Оставить инициатором согласования {автор
        // черновика}", а не "Стать инициатором" - см. actingOnSomeoneElsesDraft/initiatorUserId
        // выше) - это стоит явно отметить в записи, иначе в истории/"Последней активности" было
        // не отличить такой запуск от обычного, где запускающий и инициатор - один и тот же
        // человек.
        var startedOnBehalfOfInitiator = initiatorUserId != currentUserId;
        string? initiatorName = null;
        if (startedOnBehalfOfInitiator)
        {
            var initiatorUser = await _db.Users.FindAsync(initiatorUserId);
            initiatorName = initiatorUser?.FullName ?? "—";
        }

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ProcessStarted, vndId, vnd.Code,
            currentUserId,
            startedOnBehalfOfInitiator
                ? new ActivityText(
                    $"{actorName} запустил(а) согласование редакции {lastRedaction.Code} ВНД «{vnd.TitleRu}» " +
                    $"от имени инициатора {initiatorName} (главный редактор, чужой черновик)",
                    $"{actorName} started approval of revision {lastRedaction.Code} of VND \"{vnd.TitleRu}\" " +
                    $"on behalf of initiator {initiatorName} (chief editor, someone else's draft)",
                    $"{actorName} «{vnd.TitleRu}» ВНДисинин {lastRedaction.Code} редакциясын {initiatorName} " +
                    "демилгечисинин атынан макулдашууну баштады (башкы редактор, бөтөн долбоор)")
                : new ActivityText(
                    $"{actorName} запустил(а) согласование редакции {lastRedaction.Code} ВНД «{vnd.TitleRu}»",
                    $"{actorName} started approval of revision {lastRedaction.Code} of VND \"{vnd.TitleRu}\"",
                    $"{actorName} «{vnd.TitleRu}» ВНДисинин {lastRedaction.Code} редакциясын макулдашууну баштады"),
            $"/base-vnd/{vndId}");
        await _db.SaveChangesAsync();

        // --- Уведомления: задача на первичное согласование - только тем, кому реально нужно
        // принять решение. Раньше здесь исключался currentUserId целиком (тот, кто нажал кнопку
        // запуска) - это было равнозначно "исключаем автоматически согласованные этапы" только
        // пока автосогласование само проверялось по currentUserId. Теперь (см. фикс выше)
        // автосогласование зависит от initiatorUserId, поэтому и здесь фильтруем по факту
        // решения (PrimaryDecision), а не по currentUserId - иначе главный редактор, запустивший
        // согласование чужого черновика и оставшийся при этом обычным (не автосогласованным)
        // участником одного из фиксированных этапов, вообще не получил бы задачу на это
        // согласование.
        var pendingApproverIds = stages
            .Where(s => s.PrimaryDecision == ApprovalStageDecision.Pending)
            .Select(s => s.ApproverUserId)
            .Distinct()
            .ToArray();

        if (pendingApproverIds.Length > 0)
            await NotifyAsync(
                VndApprovalNotificationMessages.TaskPrimaryApproval(lastRedaction.Code, vnd.TitleRu),
                NotificationCategory.Vnd, vndId, currentUserId, pendingApproverIds);

        // --- Уведомление инициатору (и ответственному за актуализацию, если согласование запущено
        // в рамках открытого цикла актуализации): редакция отправлена на согласование.
        // Плюс автор самого черновика (vnd.CreatedByUserId) - раньше не уведомлялся вовсе, если
        // согласование запускал кто-то другой (см. actingOnSomeoneElsesDraft выше): "Ваша
        // редакция... отправлена на согласование" уходило только тому, кто нажал кнопку, даже
        // если редакция не его. NotifyAsync сам убирает дубликаты (Distinct() на UserIds), так
        // что если это один и тот же человек - лишнего уведомления не будет.
        var sentToApprovalRecipients = new List<int> { currentUserId };
        if (vnd.ActualizationResponsibleUserId.HasValue)
            sentToApprovalRecipients.Add(vnd.ActualizationResponsibleUserId.Value);
        if (draftOwnerId.HasValue)
            sentToApprovalRecipients.Add(draftOwnerId.Value);

        await NotifyAsync(
            VndApprovalNotificationMessages.SentToApproval(lastRedaction.Code, vnd.TitleRu),
            NotificationCategory.Vnd, vndId, currentUserId,
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

    /// <summary>История ВСЕХ процессов согласования этого ВНД — по всем редакциям и циклам
    /// актуализации, включая уже завершённые/отозванные/отклонённые (в отличие от
    /// GetByVndIdAsync выше, который отдаёт только процесс последней редакции).</summary>
    public async Task<List<ApprovalProcessResponse>> GetHistoryByVndIdAsync(int vndId)
    {
        var processIds = await _db.VndApprovalProcesses
            .Where(x => x.VndId == vndId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Id)
            .ToListAsync();

        var result = new List<ApprovalProcessResponse>(processIds.Count);
        foreach (var id in processIds)
            result.Add(await LoadResponseAsync(id));
        return result;
    }

    public async Task<ApprovalProcessResponse> DecideAsync(
        int vndId, int stageId, ApprovalDecisionRequest request, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        var stage = process.Stages.FirstOrDefault(x => x.Id == stageId)
                    ?? throw new KeyNotFoundException($"Этап согласования с id={stageId} не найден");

        if (stage.ApproverUserId != currentUserId)
            throw new UnauthorizedAccessException("Вы не назначены согласующим на этом этапе");

        if (stage.IsRemovedByEditor)
            throw new InvalidOperationException(
                "Вы исключены из маршрута согласования главным редактором — решение принять нельзя");

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

        var quotes = ParseQuotes(request.QuotesJson);

        // Версия документа редакции, к которой относится это решение (и его цитаты) - см.
        // VndApprovalStageQuote.RevisionIndex - снимается ДО того, как решение будет записано,
        // тем же способом, что и SnapshotNumber в ResubmitAfterRevisionAsync (количество уже
        // существующих снимков этой редакции).
        var revisionIndex = await _db.Set<VndRedactionRevisionSnapshot>()
            .Where(s => s.VndRedactionId == process.RedactionId)
            .CountAsync();

        var decision = request.Decision switch
        {
            ApprovalDecisionType.Approve => ApprovalStageDecision.Approved,
            ApprovalDecisionType.ApproveWithComment => ApprovalStageDecision.ApprovedWithComment,
            ApprovalDecisionType.Reject => ApprovalStageDecision.Rejected,
            _ => throw new InvalidOperationException("Неизвестный тип решения")
        };

        try
        {
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
                AttachDecisionQuotes(stage, ApprovalStagePhase.Primary, quotes, revisionIndex);

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
                AttachDecisionQuotes(stage, ApprovalStagePhase.Repeat, quotes, revisionIndex);

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
                AttachDecisionQuotes(stage, ApprovalStagePhase.FinalHold, quotes, revisionIndex);

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
        }
        catch (DbUpdateConcurrencyException)
        {
            // Этап сверяется по xmin (см. UseXminAsConcurrencyToken в
            // VndApprovalStageConfiguration) - сюда попадаем, если состояние этапа успело
            // измениться (чаще всего: сработал фоновый автоакцепт по таймауту,
            // см. ProcessTimeoutsAsync/TrySaveTimeoutBatchAsync) в промежутке между тем, как
            // была открыта эта страница, и нажатием кнопки решения. Без этой проверки решение
            // согласующего могло молча перезаписать уже свершившийся автоакцепт (или наоборот).
            throw new InvalidOperationException(
                "Не удалось сохранить решение — статус этапа согласования уже изменился " +
                "(например, истёк срок и применён автоакцепт по таймауту). Обновите страницу.");
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

        // Раньше сюда, прямо в ActivityLog, подставлялся текст комментария/замечания
        // согласующего (сначала - целиком, до MaxResolutionCommentLength = 35000 символов,
        // затем - хотя бы пометкой "(с замечанием)"). В ленте "Последняя активность" любое
        // упоминание комментария/замечания к резолюции не нужно вовсе - лента показывает
        // только сам факт решения (согласовано/отклонено), без каких-либо следов текста
        // замечания. Сам текст замечания по-прежнему доступен на карточке ВНД (в ходе
        // согласования) и в уведомлении, которое приходит инициатору (см.
        // VndApprovalNotificationMessages).
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
            decisionNotice, NotificationCategory.Vnd, vndId, currentUserId, process.InitiatorUserId);

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

        await CancelInternalAsync(process, currentUserId);
        return await LoadResponseAsync(process.Id);
    }

    public async Task WithdrawForCancelAsync(int vndId, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);
        await CancelInternalAsync(process, currentUserId);
    }

    /// <summary>Общая часть отзыва согласования — используется и обычным CancelAsync
    /// (с проверкой прав инициатора/CancelAnyVndApproval), и WithdrawForCancelAsync
    /// (вызывается изнутри архивации ВНД, где авторизация уже выполнена отдельным правом
    /// CancelVnd, см. VndService.CancelAsync).</summary>
    private async Task CancelInternalAsync(VndApprovalProcess process, int currentUserId)
    {
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
        var vndId = vnd.Id;

        // Редакция снова становится черновиком (её можно править, переотправить или удалить).
        redaction.ApprovalStatus = RedactionApprovalStatus.Draft;

        // Документ: если это была первая редакция — возвращаем в черновик; если это цикл
        // актуализации существующего ВНД — возвращаем на актуализацию, а не в черновик.
        // (Если это вызвано архивацией — VndService.CancelAsync сразу следом перезапишет
        // Status на Archived; этот промежуточный переход нужен только затем, чтобы редакция
        // и документ синхронно вышли из "На согласовании" тем же путём, что и при обычном
        // отзыве согласования.)
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
            .Where(s => !s.IsRemovedByEditor)
            .Select(s => s.ApproverUserId)
            .Where(id => id != currentUserId)
            .Distinct()
            .ToArray();

        await NotifyAsync(
            VndApprovalNotificationMessages.Cancelled(redaction.Code, vnd.TitleRu),
            NotificationCategory.Vnd, vndId, currentUserId, approverIds);

        // Отзывает не сам инициатор, а кто-то другой (главный редактор с правом
        // CancelAnyVndApproval, см. CancelAsync выше) - инициатору отдельное уведомление:
        // рассылка approverIds выше про его собственное согласование не говорит вообще ничего
        // (он там не участвует как согласующий), так что без этого он никак не узнал бы, что
        // его процесс отозвали не он сам.
        if (process.InitiatorUserId != currentUserId)
            await NotifyAsync(
                VndApprovalNotificationMessages.CancelledByOther(actorName, redaction.Code, vnd.TitleRu),
                NotificationCategory.Vnd, vndId, currentUserId, process.InitiatorUserId);
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

        if (request.RemarksAgreement != RemarksAgreement.FullyAgree && request.DisagreementMatrix is null)
            throw new InvalidOperationException(
                "Если вы не согласны со всеми замечаниями (полностью или частично), приложите матрицу " +
                "разногласий — сформируйте её в системе или загрузите готовый файл");

        if (request.Comment is { Length: > MaxResolutionCommentLength })
            throw new InvalidOperationException(
                $"Комментарий не может превышать {MaxResolutionCommentLength} символов");

        if (request.CommentAttachments is { Count: > MaxResolutionAttachments })
            throw new InvalidOperationException(
                $"К комментарию нельзя приложить больше {MaxResolutionAttachments} файлов");

        var oversizedCommentFile = request.CommentAttachments?.FirstOrDefault(f => f.Length > MaxResolutionAttachmentSizeBytes);
        if (oversizedCommentFile is not null)
            throw new InvalidOperationException(
                $"Файл «{oversizedCommentFile.FileName}» превышает максимальный размер " +
                $"{MaxResolutionAttachmentSizeBytes / 1024 / 1024} МБ на один файл");

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

        // Снимок файлов редакции ДО перезаписи ниже — чтобы проверяющие могли скачать и
        // сравнить версию, к которой относились их замечания, с исправленной (см.
        // VndRedactionRevisionSnapshot). Phase/RoundNumber снимка = круг, который сейчас
        // завершается этой отправкой - тот же расчёт, что чуть ниже использует
        // SnapshotPhaseRoundIfNeededAsync для VndApprovalPhaseRound (см.
        // DetermineActiveRevisionPhaseAsync). Снимаем безусловно, на каждую отправку - даже
        // если по факту ни один файл не был заменён этим кругом, это всё равно отдельная
        // пронумерованная версия ("10296-Р1.1", "10296-Р1.2" и т.д.), с которой инициатор
        // ответил на замечания круга.
        var (snapshotPhase, snapshotRoundNumber) = await DetermineActiveRevisionPhaseAsync(process);
        var snapshotNumber = await _db.Set<VndRedactionRevisionSnapshot>()
            .Where(s => s.VndRedactionId == redaction.Id)
            .CountAsync() + 1;
        _db.Set<VndRedactionRevisionSnapshot>().Add(new VndRedactionRevisionSnapshot
        {
            VndRedactionId = redaction.Id,
            ApprovalProcessId = process.Id,
            SnapshotNumber = snapshotNumber,
            Phase = snapshotPhase,
            RoundNumber = snapshotRoundNumber,
            DocFileRuId = redaction.DocFileRuId,
            DocFileKgId = redaction.DocFileKgId,
            DocFileEnId = redaction.DocFileEnId,
            TidFileId = redaction.TidFileId,
            DisagreementMatrixFileId = redaction.DisagreementMatrixFileId,
            CreatedAt = resubmittedAt,
        });

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

        if (request.DisagreementMatrix is not null)
        {
            var saved = await _fileService.SaveAsync(request.DisagreementMatrix, currentUserId);
            redaction.DisagreementMatrixFileId = saved.Id;
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

        // Снимок предыдущего круга "Повторного согласования" (комментарий инициатора + решения
        // согласующих) - берём ДО перезаписи ниже, иначе снимать будет уже нечего. См.
        // VndApprovalPhaseRound - без этого при нескольких кругах доработки подряд в одном и
        // том же процессе согласования история промежуточных кругов терялась безвозвратно.
        var previousRepeatInitiatorComment = process.RepeatInitiatorComment;
        var previousRepeatStartedAt = process.RepeatStartedAt;

        process.RepeatInitiatorComment = request.Comment;

        // Комментарий полностью перезаписывается на каждый круг доработки - вложения к
        // предыдущему комментарию больше не актуальны, удаляем вместе с файлами в хранилище
        // и заменяем набором, пришедшим с этой отправкой (см. VndRepeatCommentAttachment).
        var oldCommentAttachments = await _db.Set<VndRepeatCommentAttachment>()
            .Where(a => a.VndApprovalProcessId == process.Id)
            .ToListAsync();
        foreach (var old in oldCommentAttachments)
        {
            try
            {
                await _fileService.DeleteAsync(old.FileAttachmentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Не удалось удалить файл {FileId} предыдущего вложения комментария процесса {ProcessId}",
                    old.FileAttachmentId, process.Id);
            }
        }
        _db.Set<VndRepeatCommentAttachment>().RemoveRange(oldCommentAttachments);

        foreach (var file in request.CommentAttachments ?? [])
        {
            if (file.Length == 0) continue;
            var saved = await _fileService.SaveAsync(file, currentUserId);
            process.RepeatInitiatorCommentAttachments.Add(new VndRepeatCommentAttachment
            {
                FileAttachmentId = saved.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (request.RemarksAgreement == RemarksAgreement.FullyAgree)
        {
            // Сохраняем круг, который сейчас будет перезаписан (см. комментарий у
            // previousRepeatInitiatorComment выше) - именно ЗДЕСЬ, а не безусловно перед if,
            // потому что при первой отправке после первичного согласования (repeat-круга ещё
            // не было) снимать нечего - SnapshotPhaseRoundIfNeededAsync сама это определяет и
            // тогда ничего не создаёт.
            await SnapshotPhaseRoundIfNeededAsync(
                process, ApprovalStagePhase.Repeat, previousRepeatStartedAt,
                previousRepeatInitiatorComment, process.Stages.Where(s => s.ParticipatesInRepeat));

            // Замечания исправлены - обычное повторное согласование (только с теми, кто участвует в repeat)
            foreach (var stage in process.Stages.Where(s => s.ParticipatesInRepeat))
            {
                // Вложения/цитаты предыдущего круга повторного согласования этого этапа больше
                // не относятся к решению, которое согласующий сейчас примет заново - без этой
                // очистки они оставались привязанными к той же фазе (Repeat) и отображались
                // рядом с текстом НОВОГО решения, как будто были приложены к нему.
                await ClearPreviousRoundArtifactsAsync(stage, ApprovalStagePhase.Repeat);

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
                NotificationCategory.Vnd, vndId, currentUserId, repeatApproverIds);

            // Count == 0 - все, кто оставлял замечания, успели быть убраны/заменены главным
            // редактором, пока шла доработка: ждать решения не от кого, иначе процесс висел бы в
            // "Повторном согласовании" до дедлайна.
            var repeatStages = process.Stages.Where(s => s.ParticipatesInRepeat).ToList();
            if (repeatStages.All(s =>
                    s.RepeatDecision is not null && s.RepeatDecision != ApprovalStageDecision.Pending))
            {
                await CompleteRepeatPhaseAsync(process);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            // PartiallyAgree или FullyDisagree - в обоих случаях составлена матрица разногласий,
            // повторное согласование пропускаем, сразу идём на финальную выдержку (решение
            // по-прежнему требуется только от тех, кто ещё не давал чистого согласования этой
            // редакции - см. ResetFinalHoldDecisionsAsync). При PartiallyAgree редакция при этом могла
            // быть обновлена файлами выше - по маршруту согласования разницы с FullyDisagree нет.
            process.Status = ApprovalProcessStatus.FinalHold;
            await ResetFinalHoldDecisionsAsync(process);
            process.FinalHoldStartedAt = DateTime.UtcNow;

            // См. комментарий выше - тот же самообход для финальной выдержки.
            AutoApproveInitiatorStages(process, ApprovalStagePhase.FinalHold);

            await _db.SaveChangesAsync();

            // См. тот же фильтр в CompleteRepeatPhaseAsync - убранные главным редактором этапы
            // не участвуют в финальной выдержке и не получают уведомление о ней.
            var stageApproverIds = process.Stages
                .Where(s => !s.IsRemovedByEditor)
                .Select(s => s.ApproverUserId)
                .ToArray();

            await NotifyAsync(
                VndApprovalNotificationMessages.FinalHoldForApprovers(redaction.Code, process.Vnd!.TitleRu),
                NotificationCategory.Vnd, vndId, currentUserId, stageApproverIds);

            await NotifyAsync(
                VndApprovalNotificationMessages.SentToFinalHold(redaction.Code),
                NotificationCategory.Vnd, vndId, currentUserId, currentUserId);

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

    public async Task<DisagreementMatrixRowResponse> UpdateDisagreementMatrixRowAsync(
        int vndId, int rowId, UpdateDisagreementMatrixRowRequest request, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        if (process.InitiatorUserId != currentUserId)
            throw new UnauthorizedAccessException("Изменять строки матрицы разногласий может только инициатор");

        if (process.Status != ApprovalProcessStatus.RevisionNeeded)
            throw new InvalidOperationException(
                "Матрицу разногласий можно редактировать только в статусе \"требуются правки\"");

        var row = await _db.Set<VndDisagreementMatrixRow>()
                      .FirstOrDefaultAsync(x => x.Id == rowId && x.ApprovalProcessId == process.Id)
                  ?? throw new KeyNotFoundException($"Строка матрицы разногласий с id={rowId} не найдена");

        row.DeveloperPosition = request.DeveloperPosition;
        row.OpponentPosition = request.OpponentPosition;
        row.DeveloperJustification = request.DeveloperJustification;

        await _db.SaveChangesAsync();

        return ToDisagreementRowResponse(row);
    }

    /// <summary>Главный редактор добавляет согласующего в уже запущенный процесс согласования —
    /// маршрут редактируется на лету, без остановки согласования. Новый этап встраивается в ту
    /// фазу, которая сейчас активна: более ранние фазы, которые процесс уже прошёл, для него
    /// технически "пропущены" (Approved, с пояснением) — ждать от него решения по фазе, которая
    /// уже закрылась для всех остальных, смысла нет.</summary>
    public async Task<ApprovalProcessResponse> AddApproverAsync(
        int vndId, AddApprovalStageRequest request, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        if (!_currentUser.HasPermission(PermissionCode.EditAnyVndApprovalRoute))
            throw new UnauthorizedAccessException(
                "Редактировать маршрут согласования может только главный редактор");

        if (process.Status is ApprovalProcessStatus.Approved
            or ApprovalProcessStatus.Cancelled
            or ApprovalProcessStatus.Rejected)
            throw new InvalidOperationException("Согласование уже завершено — маршрут менять нельзя");

        var approver = await _db.Users
                           .Include(u => u.Roles)
                           .FirstOrDefaultAsync(u => u.Id == request.ApproverUserId)
                       ?? throw new KeyNotFoundException(
                           $"Пользователь с id={request.ApproverUserId} не найден");

        if (!approver.Roles.SelectMany(r => r.PermissionCodes).Contains((int)PermissionCode.ActAsApprover))
            throw new InvalidOperationException("У этого пользователя нет права выступать в роли согласующего");

        // Уже действующий этап на этого же пользователя - нельзя завести второй активный
        // одновременно. Ранее убранный этап (IsRemovedByEditor) этому не мешает - см. пояснение
        // у RemoveApproverAsync ниже: старая запись остаётся в истории, а этот вызов заведёт
        // для того же человека новый, отдельный этап.
        if (process.Stages.Any(s => s.ApproverUserId == approver.Id && !s.IsRemovedByEditor))
            throw new InvalidOperationException("Этот пользователь уже согласует эту редакцию");

        if (approver.OrgUnitId is null)
            throw new InvalidOperationException(
                "У пользователя не указано структурное подразделение — его нельзя назначить согласующим");

        var now = DateTime.UtcNow;
        var nextOrder = process.Stages.Count == 0 ? 1 : process.Stages.Max(s => s.Order) + 1;

        var stage = new VndApprovalStage
        {
            ApprovalProcessId = process.Id,
            Order = nextOrder,
            Kind = ApprovalStageKind.Custom,
            Title = "Доп. согласующий",
            CoordinationStageId = null,
            OrgUnitId = approver.OrgUnitId.Value,
            ApproverUserId = approver.Id,
        };

        // В какую фазу встраивается новый этап - определяется текущим статусом процесса. Фазы,
        // которые процесс уже ПРОШЁЛ к этому моменту, для нового этапа считаются пропущенными
        // (Approved, с явным пояснением) - он появился позже и не должен вечно висеть Pending по
        // фазе, которая для него никогда не наступит.
        switch (process.Status)
        {
            case ApprovalProcessStatus.Primary:
                // Первичная фаза как раз идёт - обычный этап, решение ожидается как у всех.
                break;

            case ApprovalProcessStatus.RevisionNeeded:
                // Ничья фаза сейчас не активна (инициатор ещё готовит исправления) - следующая
                // активная фаза зависит от того, как он ответит на замечания при повторной
                // отправке (ResubmitAfterRevisionAsync): обычная доработка → Repeat,
                // несогласие/частичное согласие → сразу FinalHold. Ставим ParticipatesInRepeat
                // на "участвует" - если отправка всё же пойдёт мимо Repeat, это никак не мешает:
                // финальная выдержка (ResetFinalHoldDecisionsAsync) берёт согласующих из ВСЕХ
                // непропущенных этапов процесса, а не только из ParticipatesInRepeat.
                stage.PrimaryDecision = ApprovalStageDecision.Approved;
                stage.PrimaryComment = SkippedPhaseComment;
                stage.PrimaryDecidedAt = now;
                stage.ParticipatesInRepeat = true;
                break;

            case ApprovalProcessStatus.Repeated:
                stage.PrimaryDecision = ApprovalStageDecision.Approved;
                stage.PrimaryComment = SkippedPhaseComment;
                stage.PrimaryDecidedAt = now;
                stage.ParticipatesInRepeat = true;
                stage.RepeatDecision = ApprovalStageDecision.Pending;
                break;

            case ApprovalProcessStatus.FinalHold:
                stage.PrimaryDecision = ApprovalStageDecision.Approved;
                stage.PrimaryComment = SkippedPhaseComment;
                stage.PrimaryDecidedAt = now;
                stage.ParticipatesInRepeat = false;
                stage.FinalHoldDecision = ApprovalStageDecision.Pending;
                break;

            default:
                throw new InvalidOperationException("В текущем статусе процесса маршрут менять нельзя");
        }

        // Главный редактор добавил в маршрут самого инициатора согласования (типичный сценарий:
        // инициатор был согласующим на обязательном этапе, редактор заменил его там на другого
        // человека и вернул инициатора доп. этапом) - решение инициатора по активной фазе, как и
        // при старте процесса (см. StartAsync), проставляется автоматически. Иначе этап висит
        // "В ожидании": у инициатора на карточке нет панели резолюции (он инициатор, а не
        // согласующий), и фаза не может завершиться до дедлайна.
        AutoApproveIfInitiator(process, stage, now);

        _db.Set<VndApprovalStage>().Add(stage);
        // EF обычно сам подхватывает новый этап в process.Stages (fixup по ApprovalProcessId),
        // но проверки AdvanceAfterRouteChangeAsync ниже должны видеть его гарантированно.
        if (!process.Stages.Contains(stage)) process.Stages.Add(stage);

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";
        var vnd = process.Vnd!;
        var redaction = process.Redaction!;

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ApproverAdded, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} добавил(а) согласующего {approver.FullName} в маршрут согласования " +
                $"редакции {redaction.Code} ВНД «{vnd.TitleRu}» (главный редактор)",
                $"{actorName} added approver {approver.FullName} to the approval route of revision " +
                $"{redaction.Code} of VND \"{vnd.TitleRu}\" (chief editor)",
                $"{actorName} башкы редактор катары «{vnd.TitleRu}» ВНДисинин {redaction.Code} " +
                $"редакциясынын макулдашуу маршрутуна {approver.FullName} макулдашуучусун кошту"),
            $"/base-vnd/{vndId}");

        await _db.SaveChangesAsync();

        // Уведомление о задаче - только если для нового этапа сейчас реально открыта фаза
        // (Primary/Repeat/FinalHold Decision == Pending), а не пропущена как более ранняя.
        var hasImmediateTask = stage.PrimaryDecision == ApprovalStageDecision.Pending
                                || stage.RepeatDecision == ApprovalStageDecision.Pending
                                || stage.FinalHoldDecision == ApprovalStageDecision.Pending;
        if (hasImmediateTask)
            await NotifyAsync(
                VndApprovalNotificationMessages.AddedAsApprover(actorName, redaction.Code, vnd.TitleRu),
                NotificationCategory.Vnd, vndId, currentUserId, approver.Id);

        // Если новый этап сразу оказался решённым (автосогласование инициатора) - фаза могла
        // закрыться, переходим дальше, не дожидаясь дедлайна.
        await AdvanceAfterRouteChangeAsync(process);

        return await LoadResponseAsync(process.Id);
    }

    /// <summary>Главный редактор убирает согласующего из уже запущенного процесса согласования.
    /// Этап не удаляется из маршрута - история согласования (в т.ч. уже принятое им решение,
    /// если оно было) не должна пропадать - а помечается недействующим (IsRemovedByEditor):
    /// задача с него снимается принудительной установкой ApprovalStageDecision.RemovedByEditor
    /// на ту фазу, что сейчас активна, ParticipatesInRepeat принудительно сбрасывается в false -
    /// и он больше никогда не участвует ни в одной последующей фазе/круге (см. проверку
    /// IsRemovedByEditor в ResetFinalHoldDecisionsAsync).</summary>
    public async Task<ApprovalProcessResponse> RemoveApproverAsync(
        int vndId, int stageId, RemoveApprovalStageRequest request, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        if (!_currentUser.HasPermission(PermissionCode.EditAnyVndApprovalRoute))
            throw new UnauthorizedAccessException(
                "Редактировать маршрут согласования может только главный редактор");

        if (process.Status is ApprovalProcessStatus.Approved
            or ApprovalProcessStatus.Cancelled
            or ApprovalProcessStatus.Rejected)
            throw new InvalidOperationException("Согласование уже завершено — маршрут менять нельзя");

        var stage = process.Stages.FirstOrDefault(x => x.Id == stageId)
                    ?? throw new KeyNotFoundException($"Этап согласования с id={stageId} не найден");

        if (stage.IsRemovedByEditor)
            throw new InvalidOperationException("Этот согласующий уже убран из маршрута");

        // Обязательный этап (Fixed - построенный из справочника coordination-users, а также
        // legacy Legal/RiskManagement/Compliance/Methodology у старых маршрутов) этим способом не
        // убирается - только Custom, добавленный вручную. Заменить согласующего на обязательном
        // этапе, не теряя сам этап, можно через ReplaceApproverAsync ниже.
        if (stage.Kind != ApprovalStageKind.Custom)
            throw new InvalidOperationException(
                "Обязательный этап маршрута нельзя убрать — замените согласующего вместо этого");

        // В маршруте должен остаться хотя бы один действующий этап - иначе согласование
        // застынет навсегда, не имея от кого дожидаться решения.
        if (process.Stages.Count(s => !s.IsRemovedByEditor) <= 1)
            throw new InvalidOperationException(
                "Нельзя убрать последнего действующего согласующего в маршруте — добавьте " +
                "другого, прежде чем убирать этого");

        var now = DateTime.UtcNow;
        stage.IsRemovedByEditor = true;
        // Навсегда исключаем из повторного согласования - независимо от того, на какой фазе
        // случилось удаление (см. подробности в комментарии к IsRemovedByEditor на модели).
        stage.ParticipatesInRepeat = false;

        // Задача снимается именно на той фазе, что сейчас активна для процесса - остальные две
        // либо уже прошли (там решение, если оно было принято, остаётся как есть - это история),
        // либо ещё не наступили и наступить для этого этапа уже не должны.
        switch (process.Status)
        {
            case ApprovalProcessStatus.Primary:
                stage.PrimaryDecision = ApprovalStageDecision.RemovedByEditor;
                stage.PrimaryDecidedAt = now;
                break;

            case ApprovalProcessStatus.RevisionNeeded:
                // Ничья фаза сейчас не активна - следующая, которая для этого этапа наступила бы
                // (Repeat или, если решение по нему уже принято, FinalHold), закрывается сразу
                // отсюда же, чтобы не оставлять её "подвешенной" до того момента, когда процесс
                // реально туда перейдёт.
                if (stage.RepeatDecision is null || stage.RepeatDecision == ApprovalStageDecision.Pending)
                {
                    stage.RepeatDecision = ApprovalStageDecision.RemovedByEditor;
                    stage.RepeatDecidedAt = now;
                }
                else if (stage.FinalHoldDecision is null || stage.FinalHoldDecision == ApprovalStageDecision.Pending)
                {
                    stage.FinalHoldDecision = ApprovalStageDecision.RemovedByEditor;
                    stage.FinalHoldDecidedAt = now;
                }
                break;

            case ApprovalProcessStatus.Repeated:
                stage.RepeatDecision = ApprovalStageDecision.RemovedByEditor;
                stage.RepeatDecidedAt = now;
                break;

            case ApprovalProcessStatus.FinalHold:
                stage.FinalHoldDecision = ApprovalStageDecision.RemovedByEditor;
                stage.FinalHoldDecidedAt = now;
                break;
        }

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";
        var vnd = process.Vnd!;
        var redaction = process.Redaction!;
        var removedApproverName = stage.ApproverUser?.FullName
            ?? (await _db.Users.FindAsync(stage.ApproverUserId))?.FullName ?? "—";

        var reasonSuffix = string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Причина: «{request.Reason}».";

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ApproverRemoved, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} убрал(а) согласующего {removedApproverName} из маршрута согласования " +
                $"редакции {redaction.Code} ВНД «{vnd.TitleRu}» (главный редактор).{reasonSuffix}",
                $"{actorName} removed approver {removedApproverName} from the approval route of revision " +
                $"{redaction.Code} of VND \"{vnd.TitleRu}\" (chief editor).",
                $"{actorName} башкы редактор катары «{vnd.TitleRu}» ВНДисинин {redaction.Code} " +
                $"редакциясынын макулдашуу маршрутунан {removedApproverName} макулдашуучусун алып салды."),
            $"/base-vnd/{vndId}");

        await _db.SaveChangesAsync();

        await NotifyAsync(
            VndApprovalNotificationMessages.RemovedFromRouteByEditor(actorName, redaction.Code, vnd.TitleRu),
            NotificationCategory.Vnd, vndId, currentUserId, stage.ApproverUserId);

        // Если убранный был последним, чьё решение ждали на текущей фазе - фаза теперь решена
        // всеми, кто остался, и можно перейти дальше, не дожидаясь дедлайна (то же самое, что
        // происходит после обычного DecideAsync).
        await AdvanceAfterRouteChangeAsync(process);

        return await LoadResponseAsync(process.Id);
    }

    /// <summary>Главный редактор заменяет согласующего на обязательном этапе маршрута — см.
    /// IVndApprovalService.ReplaceApproverAsync. Реализация сознательно повторяет две половины
    /// RemoveApproverAsync (снятие задачи по текущей фазе со старой записи) и AddApproverAsync
    /// (встраивание новой записи в текущую активную фазу) - вместо вызова их напрямую, чтобы не
    /// проходить дважды проверки прав/статуса процесса и не задваивать запись в БД лишним
    /// промежуточным сохранением.</summary>
    public async Task<ApprovalProcessResponse> ReplaceApproverAsync(
        int vndId, int stageId, ReplaceApprovalStageRequest request, int currentUserId)
    {
        var process = await LoadProcessForVndAsync(vndId);

        if (!_currentUser.HasPermission(PermissionCode.EditAnyVndApprovalRoute))
            throw new UnauthorizedAccessException(
                "Редактировать маршрут согласования может только главный редактор");

        if (process.Status is ApprovalProcessStatus.Approved
            or ApprovalProcessStatus.Cancelled
            or ApprovalProcessStatus.Rejected)
            throw new InvalidOperationException("Согласование уже завершено — маршрут менять нельзя");

        var oldStage = process.Stages.FirstOrDefault(x => x.Id == stageId)
                       ?? throw new KeyNotFoundException($"Этап согласования с id={stageId} не найден");

        if (oldStage.IsRemovedByEditor)
            throw new InvalidOperationException("Этот согласующий уже убран из маршрута");

        var newApprover = await _db.Users
                               .Include(u => u.Roles)
                               .FirstOrDefaultAsync(u => u.Id == request.NewApproverUserId)
                           ?? throw new KeyNotFoundException(
                               $"Пользователь с id={request.NewApproverUserId} не найден");

        if (!newApprover.Roles.SelectMany(r => r.PermissionCodes).Contains((int)PermissionCode.ActAsApprover))
            throw new InvalidOperationException("У этого пользователя нет права выступать в роли согласующего");

        if (newApprover.OrgUnitId is null)
            throw new InvalidOperationException(
                "У пользователя не указано структурное подразделение — его нельзя назначить согласующим");

        if (newApprover.Id == oldStage.ApproverUserId)
            throw new InvalidOperationException("Этот пользователь уже согласует на этом этапе");

        // Тот же человек не может держать два действующих этапа одновременно - та же проверка,
        // что и в AddApproverAsync.
        if (process.Stages.Any(s =>
                s.Id != oldStage.Id && s.ApproverUserId == newApprover.Id && !s.IsRemovedByEditor))
            throw new InvalidOperationException("Этот пользователь уже согласует эту редакцию");

        var now = DateTime.UtcNow;

        // 1. Убираем старую запись - та же логика снятия задачи по активной сейчас фазе, что и в
        // RemoveApproverAsync выше (история его решений на пройденных фазах остаётся как есть).
        oldStage.IsRemovedByEditor = true;
        oldStage.ParticipatesInRepeat = false;

        switch (process.Status)
        {
            case ApprovalProcessStatus.Primary:
                oldStage.PrimaryDecision = ApprovalStageDecision.RemovedByEditor;
                oldStage.PrimaryDecidedAt = now;
                break;

            case ApprovalProcessStatus.RevisionNeeded:
                if (oldStage.RepeatDecision is null || oldStage.RepeatDecision == ApprovalStageDecision.Pending)
                {
                    oldStage.RepeatDecision = ApprovalStageDecision.RemovedByEditor;
                    oldStage.RepeatDecidedAt = now;
                }
                else if (oldStage.FinalHoldDecision is null
                         || oldStage.FinalHoldDecision == ApprovalStageDecision.Pending)
                {
                    oldStage.FinalHoldDecision = ApprovalStageDecision.RemovedByEditor;
                    oldStage.FinalHoldDecidedAt = now;
                }
                break;

            case ApprovalProcessStatus.Repeated:
                oldStage.RepeatDecision = ApprovalStageDecision.RemovedByEditor;
                oldStage.RepeatDecidedAt = now;
                break;

            case ApprovalProcessStatus.FinalHold:
                oldStage.FinalHoldDecision = ApprovalStageDecision.RemovedByEditor;
                oldStage.FinalHoldDecidedAt = now;
                break;
        }

        // 2. Заводим новую запись с тем же Kind/CoordinationStageId/Title, что и у старой (маршрут
        // по существу не меняется - меняется только исполнитель). Order не может повторять
        // oldStage.Order: строка старого этапа не удаляется физически (IsRemovedByEditor лишь
        // прячет её из активных - история решений остаётся), а на (ApprovalProcessId, Order)
        // в БД висит уникальный индекс. Поэтому новой записи, как и в AddApproverAsync выше,
        // выделяем следующий свободный Order - она встанет в списке этапов последней; текущая
        // активная фаза подхватывает её так же, как в AddApproverAsync (более ранние пройденные
        // фазы для неё считаются пропущенными).
        var nextOrder = process.Stages.Max(s => s.Order) + 1;

        var newStage = new VndApprovalStage
        {
            ApprovalProcessId = process.Id,
            Order = nextOrder,
            Kind = oldStage.Kind,
            Title = oldStage.Title,
            CoordinationStageId = oldStage.CoordinationStageId,
            OrgUnitId = newApprover.OrgUnitId.Value,
            ApproverUserId = newApprover.Id,
        };

        switch (process.Status)
        {
            case ApprovalProcessStatus.Primary:
                break;

            case ApprovalProcessStatus.RevisionNeeded:
                newStage.PrimaryDecision = ApprovalStageDecision.Approved;
                newStage.PrimaryComment = SkippedPhaseComment;
                newStage.PrimaryDecidedAt = now;
                newStage.ParticipatesInRepeat = true;
                break;

            case ApprovalProcessStatus.Repeated:
                newStage.PrimaryDecision = ApprovalStageDecision.Approved;
                newStage.PrimaryComment = SkippedPhaseComment;
                newStage.PrimaryDecidedAt = now;
                newStage.ParticipatesInRepeat = true;
                newStage.RepeatDecision = ApprovalStageDecision.Pending;
                break;

            case ApprovalProcessStatus.FinalHold:
                newStage.PrimaryDecision = ApprovalStageDecision.Approved;
                newStage.PrimaryComment = SkippedPhaseComment;
                newStage.PrimaryDecidedAt = now;
                newStage.ParticipatesInRepeat = false;
                newStage.FinalHoldDecision = ApprovalStageDecision.Pending;
                break;
        }

        // Новым согласующим назначен сам инициатор - см. пояснение в AddApproverAsync.
        AutoApproveIfInitiator(process, newStage, now);

        _db.Set<VndApprovalStage>().Add(newStage);
        // EF обычно сам подхватывает новый этап в process.Stages (fixup по ApprovalProcessId),
        // но проверки AdvanceAfterRouteChangeAsync ниже должны видеть его гарантированно.
        if (!process.Stages.Contains(newStage)) process.Stages.Add(newStage);

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";
        var vnd = process.Vnd!;
        var redaction = process.Redaction!;
        var oldApproverName = oldStage.ApproverUser?.FullName
            ?? (await _db.Users.FindAsync(oldStage.ApproverUserId))?.FullName ?? "—";
        var reasonSuffix = string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Причина: «{request.Reason}».";

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ApproverRemoved, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} заменил(а) согласующего {oldApproverName} на {newApprover.FullName} на этапе " +
                $"«{oldStage.Title}» в маршруте согласования редакции {redaction.Code} ВНД «{vnd.TitleRu}» " +
                $"(главный редактор).{reasonSuffix}",
                $"{actorName} replaced approver {oldApproverName} with {newApprover.FullName} on stage " +
                $"\"{oldStage.Title}\" of the approval route of revision {redaction.Code} of VND " +
                $"\"{vnd.TitleRu}\" (chief editor).",
                $"{actorName} башкы редактор катары «{vnd.TitleRu}» ВНДисинин {redaction.Code} " +
                $"редакциясынын макулдашуу маршрутундагы «{oldStage.Title}» этабында {oldApproverName} " +
                $"макулдашуучусун {newApprover.FullName} дегенге алмаштырды."),
            $"/base-vnd/{vndId}");

        await _db.SaveChangesAsync();

        await NotifyAsync(
            VndApprovalNotificationMessages.RemovedFromRouteByEditor(actorName, redaction.Code, vnd.TitleRu),
            NotificationCategory.Vnd, vndId, currentUserId, oldStage.ApproverUserId);

        var hasImmediateTask = newStage.PrimaryDecision == ApprovalStageDecision.Pending
                                || newStage.RepeatDecision == ApprovalStageDecision.Pending
                                || newStage.FinalHoldDecision == ApprovalStageDecision.Pending;
        if (hasImmediateTask)
            await NotifyAsync(
                VndApprovalNotificationMessages.AddedAsApprover(actorName, redaction.Code, vnd.TitleRu),
                NotificationCategory.Vnd, vndId, currentUserId, newApprover.Id);

        // Замена могла закрыть текущую фазу: прежний согласующий был последним, кого ждали, а
        // новый - инициатор (автосогласован) либо фаза для него уже пройдена.
        await AdvanceAfterRouteChangeAsync(process);

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
            var trackedBefore = SnapshotTrackedEntities();

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
            await TrySaveTimeoutBatchAsync(process.Id, trackedBefore);
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
            var trackedBefore = SnapshotTrackedEntities();

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
            await TrySaveTimeoutBatchAsync(process.Id, trackedBefore);
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
            var trackedBefore = SnapshotTrackedEntities();

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
            await TrySaveTimeoutBatchAsync(process.Id, trackedBefore);
        }
    }

    /// <summary>Снимок сущностей, уже отслеживаемых контекстом ДО обработки одного процесса в
    /// ProcessTimeoutsAsync — см. TrySaveTimeoutBatchAsync, которому этот снимок нужен, чтобы
    /// откатить именно то, что добавила обработка ЭТОГО процесса, и не задеть уже накопленные
    /// (или ещё не сохранённые) изменения других процессов в том же проходе.</summary>
    private HashSet<object> SnapshotTrackedEntities() =>
        new(_db.ChangeTracker.Entries().Select(e => e.Entity), ReferenceEqualityComparer.Instance);

    /// <summary>Сохраняет автоакцепт по таймауту для ОДНОГО процесса отдельным
    /// SaveChangesAsync — раньше весь пакет процессов сохранялся одним общим вызовом в конце
    /// ProcessTimeoutsAsync, и конфликт по одному документу откатил бы (в рамках одной
    /// EF-транзакции) автоакцепт и для всех остальных, уже корректно обработанных в этом же
    /// проходе.
    ///
    /// VndApprovalStage сверяется по xmin (см. UseXminAsConcurrencyToken в
    /// VndApprovalStageConfiguration) — конкурентного контроля версий тут раньше не было вовсе.
    /// Если пользователь успел принять РЕАЛЬНОЕ решение по одному из этапов этого процесса
    /// (POST .../decide) параллельно с этим фоновым проходом, который уже прочитал этап как
    /// Pending, — сохранение здесь провалится с DbUpdateConcurrencyException вместо того чтобы
    /// молча перезаписать это решение автоакцептом по таймауту. Пользовательское решение при
    /// этом уже сохранено им самим и никуда не девается; мы лишь откатываем то, что успела
    /// НАКОПИТЬ В ПАМЯТИ обработка этого процесса в фоновой джобе (новые записи журнала,
    /// изменения статуса документа/редакции и т.п. — см. SnapshotTrackedEntities), чтобы это
    /// не просочилось в сохранение следующего процесса в этом же проходе. Следующий плановый
    /// запуск ProcessTimeoutsAsync пересчитает этот процесс заново, уже по актуальным
    /// данным.</summary>
    private async Task TrySaveTimeoutBatchAsync(int processId, HashSet<object> trackedBefore)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogInformation(ex,
                "Автоакцепт по таймауту пропущен для процесса {ProcessId} — по одному из этапов " +
                "уже принято решение параллельно; следующий проход обработает его по актуальным данным",
                processId);

            foreach (var entry in _db.ChangeTracker.Entries()
                         .Where(e => !trackedBefore.Contains(e.Entity))
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
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
                NotificationCategory.Vnd, process.VndId, null, process.InitiatorUserId);
        }

        if (save) await _db.SaveChangesAsync();
    }

    private async Task CompleteRepeatPhaseAsync(VndApprovalProcess process, bool save = true)
    {
        // Зеркалит проверку в CompletePrimaryPhaseAsync: если на повторном согласовании кто-то
        // СНОВА оставил замечания (Rejected до сюда не доходит - обрабатывается отдельно и сразу
        // прекращает весь процесс, см. RejectApprovalAsync) - это ещё один круг доработки, а не
        // переход на финальную выдержку. На финальную выдержку процесс должен попадать только
        // тогда, когда очередной круг повторного согласования прошёл вообще без замечаний.
        var hasRemarks = process.Stages
            .Where(s => s.ParticipatesInRepeat)
            .Any(s => s.RepeatDecision is ApprovalStageDecision.ApprovedWithComment);

        if (hasRemarks)
        {
            process.Status = ApprovalProcessStatus.RevisionNeeded;

            await NotifyAsync(
                VndApprovalNotificationMessages.RevisionNeeded(process.Redaction!.Code, process.Vnd!.TitleRu),
                NotificationCategory.Vnd, process.VndId, null, process.InitiatorUserId);

            if (save) await _db.SaveChangesAsync();
            return;
        }

        process.Status = ApprovalProcessStatus.FinalHold;

        // ResetFinalHoldDecisionsAsync снимает историю предыдущего круга финальной выдержки
        // (если он был - см. SnapshotPhaseRoundIfNeededAsync) ДО его перезаписи, а снимок
        // включает "когда начался круг" (process.FinalHoldStartedAt) - поэтому вызывается
        // раньше, чем это поле переставится на новый круг ниже (см. тот же порядок в
        // ResubmitAfterRevisionAsync выше, откуда исходно и был скопирован этот блок).
        await ResetFinalHoldDecisionsAsync(process);
        process.FinalHoldStartedAt = DateTime.UtcNow;

        // Инициатор мог быть согласующим на одном из этапов - на финальной выдержке
        // его решение тоже проставляется автоматически, иначе оно "висит" до просрочки.
        AutoApproveInitiatorStages(process, ApprovalStagePhase.FinalHold);

        // Убранные главным редактором этапы не участвуют в финальной выдержке - см. фикс в
        // ResetFinalHoldDecisionsAsync выше, здесь дополнительно не рассылаем им уведомление о
        // ней, у них уже нет задачи по этому согласованию.
        var stageApproverIds = process.Stages
            .Where(s => !s.IsRemovedByEditor)
            .Select(s => s.ApproverUserId)
            .ToArray();

        // --- Всем согласующим: документ ушёл на финальную выдержку
        await NotifyAsync(
            VndApprovalNotificationMessages.FinalHoldForApprovers(process.Redaction!.Code, process.Vnd!.TitleRu),
            NotificationCategory.Vnd, process.VndId, null, stageApproverIds);

        // --- Инициатору: его редакция отправлена на финальную выдержку
        await NotifyAsync(
            VndApprovalNotificationMessages.SentToFinalHold(process.Redaction!.Code),
            NotificationCategory.Vnd, process.VndId, null, process.InitiatorUserId);

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
            NotificationCategory.Vnd, process.VndId, null, process.InitiatorUserId);
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
                NotificationCategory.Vnd, process.VndId, rejectedByUserId, pendingApproverIds);
    }

    private async Task FinalizeApprovalAsync(VndApprovalProcess process, bool afterRevision)
    {
        process.Status = ApprovalProcessStatus.Approved;
        process.CompletedAt = DateTime.UtcNow;

        var redaction = process.Redaction!;
        var vnd = process.Vnd!;

        redaction.ApprovalStatus = RedactionApprovalStatus.Approved;

        // Вложения к резолюциям (VndApprovalStageAttachment) и к комментарию инициатора о
        // внесённых исправлениях (VndRepeatCommentAttachment) намеренно НЕ удаляются, когда
        // редакция становится согласованной - они остаются частью истории согласования наравне
        // с текстом самих резолюций/комментария (PrimaryComment/RepeatComment/FinalHoldComment/
        // RepeatInitiatorComment).

        // Формируем Лист согласования по шаблону: название ВНД, ФИО и должность каждого
        // согласующего, единая дата (момент окончательного согласования) и результат
        // "Согласовано" для всех строк. Достижимо и из фонового таймаут-джоба
        // (ProcessTimeoutsAsync) - без HTTP-запроса, поэтому генерация полностью на сервере.
        await GenerateApprovalSheetAsync(process, redaction);

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
            notice, NotificationCategory.Vnd, process.VndId, null, consolidationRecipients.ToArray());
    }

    /// <summary>Формирует и сохраняет Лист согласования редакции (см. ApprovalSheetGenerator),
    /// заполняя его ФИО/должностью всех согласующих этого процесса (process.Stages - по одной
    /// строке на согласующего, порядок как в маршрутном листе) и датой этого момента - она общая
    /// для всех строк, т.к. фиксирует не решение конкретного согласующего, а момент, когда
    /// редакция в целом стала согласованной.</summary>
    private async Task GenerateApprovalSheetAsync(VndApprovalProcess process, VndRedaction redaction)
    {
        // Убранные главным редактором этапы не значатся "согласовавшими" - в Листе согласования
        // они не должны появляться, как будто фактически участвовали в решении.
        var approverIds = process.Stages
            .Where(s => !s.IsRemovedByEditor)
            .OrderBy(s => s.Order)
            .Select(s => s.ApproverUserId)
            .ToList();

        var usersById = await _db.Users
            .Include(u => u.Position)
            .Where(u => approverIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var approvers = approverIds
            .Select(id => usersById.TryGetValue(id, out var u)
                ? new ApprovalSheetApproverInfo(u.FullName, u.Position?.TitleRu)
                : new ApprovalSheetApproverInfo("", null))
            .ToList();

        var approvedAt = process.CompletedAt ?? DateTime.UtcNow;

        byte[] content;
        try
        {
            content = _approvalSheetGenerator.Generate(process.Vnd!.TitleRu, approvedAt, approvers);
        }
        catch (Exception ex)
        {
            // Лист согласования - вспомогательный документ; сбой его генерации не должен срывать
            // само согласование редакции (уже зафиксированное выше).
            _logger.LogError(ex, "Не удалось сформировать Лист согласования для редакции {RedactionId}",
                redaction.Id);
            return;
        }

        var fileName = $"{redaction.Code}_Лист_согласования.docx";
        const string wordContentType =
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        var saved = await _fileService.SaveGeneratedAsync(
            content, fileName, wordContentType, process.InitiatorUserId);

        redaction.ApprovalSheetFileId = saved.Id;
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
        const string comment = InitiatorAutoApprovedComment;
        var now = DateTime.UtcNow;

        IEnumerable<VndApprovalStage> stages = phase switch
        {
            ApprovalStagePhase.Repeat => process.Stages.Where(s =>
                s.ApproverUserId == process.InitiatorUserId
                && !s.IsRemovedByEditor
                && s.ParticipatesInRepeat
                && s.RepeatDecision == ApprovalStageDecision.Pending),
            ApprovalStagePhase.FinalHold => process.Stages.Where(s =>
                s.ApproverUserId == process.InitiatorUserId
                && !s.IsRemovedByEditor
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

    /// <summary>Этап, который главный редактор только что добавил/заменил в уже запущенном
    /// процессе (AddApproverAsync/ReplaceApproverAsync): если его согласующий - сам инициатор
    /// согласования, решение по активной сейчас фазе проставляется автоматически, как при старте
    /// процесса (StartAsync) и при входе в повторное согласование/финальную выдержку
    /// (AutoApproveInitiatorStages). На доработке (RevisionNeeded) активной фазы нет - там
    /// автосогласование сработает само при повторной отправке.</summary>
    private static void AutoApproveIfInitiator(VndApprovalProcess process, VndApprovalStage stage, DateTime now)
    {
        if (stage.ApproverUserId != process.InitiatorUserId) return;

        switch (process.Status)
        {
            case ApprovalProcessStatus.Primary when stage.PrimaryDecision == ApprovalStageDecision.Pending:
                stage.PrimaryDecision = ApprovalStageDecision.Approved;
                stage.PrimaryComment = InitiatorAutoApprovedComment;
                stage.PrimaryDecidedAt = now;
                stage.ParticipatesInRepeat = false;
                break;

            case ApprovalProcessStatus.Repeated when stage.RepeatDecision == ApprovalStageDecision.Pending:
                stage.RepeatDecision = ApprovalStageDecision.Approved;
                stage.RepeatComment = InitiatorAutoApprovedComment;
                stage.RepeatDecidedAt = now;
                break;

            case ApprovalProcessStatus.FinalHold when stage.FinalHoldDecision == ApprovalStageDecision.Pending:
                stage.FinalHoldDecision = ApprovalStageDecision.Approved;
                stage.FinalHoldComment = InitiatorAutoApprovedComment;
                stage.FinalHoldDecidedAt = now;
                break;
        }
    }

    /// <summary>После правки маршрута главным редактором (добавление/удаление/замена
    /// согласующего) проверяет, не оказалась ли активная фаза уже решена всеми действующими
    /// участниками, и если да - переводит процесс дальше так же, как это сделал бы DecideAsync
    /// после последнего решения. Без этого процесс "зависал" до дедлайна, например когда
    /// последний, чьего решения ждали, был заменён на инициатора (автосогласован) или убран.</summary>
    private async Task AdvanceAfterRouteChangeAsync(VndApprovalProcess process)
    {
        switch (process.Status)
        {
            case ApprovalProcessStatus.Primary
                when process.Stages.All(s => s.PrimaryDecision != ApprovalStageDecision.Pending):
                await CompletePrimaryPhaseAsync(process, save: false);
                break;

            // Пустой список участников повторного согласования (все, кто оставлял замечания,
            // убраны/заменены) - тоже повод завершить фазу: ждать решения больше не от кого.
            case ApprovalProcessStatus.Repeated
                when process.Stages.Where(s => s.ParticipatesInRepeat).All(s =>
                    s.RepeatDecision is not null && s.RepeatDecision != ApprovalStageDecision.Pending):
                await CompleteRepeatPhaseAsync(process, save: false);
                break;

            case ApprovalProcessStatus.FinalHold
                when process.Stages.All(s =>
                    s.FinalHoldDecision is not null && s.FinalHoldDecision != ApprovalStageDecision.Pending):
                await FinalizeApprovalAsync(process, afterRevision: true);
                break;
        }

        await _db.SaveChangesAsync();
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
    private async Task ResetFinalHoldDecisionsAsync(VndApprovalProcess process)
    {
        const string comment = "Согласовано автоматически";
        var now = DateTime.UtcNow;

        // Этапы, у которых сейчас реально начинается новый круг финальной выдержки (т.е. их
        // FinalHold*-поля будут перезаписаны ниже) - собираем отдельно от "чистых" этапов,
        // чтобы сначала сделать один снимок круга целиком (см. SnapshotPhaseRoundIfNeededAsync),
        // и только потом стирать данные. Если это САМЫЙ ПЕРВЫЙ заход в финальную выдержку по
        // этому процессу, у всех этапов FinalHoldDecision ещё null - снимать нечего,
        // SnapshotPhaseRoundIfNeededAsync сама в этом случае ничего не создаст.
        var stagesStartingNewRound = new List<VndApprovalStage>();

        foreach (var stage in process.Stages)
        {
            // Убранный главным редактором этап никогда не возвращается ни в один следующий круг
            // финальной выдержки — без этой проверки LatestDecisionBeforeFinalHold ниже вернул бы
            // ApprovalStageDecision.RemovedByEditor как "не чистое" решение (оно не Approved и не
            // AutoApprovedByTimeout), и цикл ниже молча сбросил бы его FinalHoldDecision в Pending,
            // возвращая уже убранного согласующего в маршрут.
            if (stage.IsRemovedByEditor)
            {
                stage.FinalHoldDecision = ApprovalStageDecision.RemovedByEditor;
                continue;
            }

            var latest = LatestDecisionBeforeFinalHold(stage);

            // Этап, добавленный главным редактором во время доработки (RevisionNeeded), ещё ни
            // разу не принимал решения сам: его PrimaryDecision = Approved - лишь технический
            // "пропуск" уже закрытой первичной фазы (SkippedPhaseComment). Без этой проверки он
            // считался бы "чисто согласовавшим" и при отправке с разногласиями сразу на финальную
            // выдержку автоматически согласовывался бы, так ни разу и не увидев документ.
            var joinedLateWithoutDecision = stage.RepeatDecision is null
                                            && stage.FinalHoldDecision is null
                                            && stage.PrimaryComment == SkippedPhaseComment;

            var wasClean = !joinedLateWithoutDecision
                           && latest is ApprovalStageDecision.Approved or ApprovalStageDecision.AutoApprovedByTimeout;

            if (wasClean)
            {
                stage.FinalHoldDecision = ApprovalStageDecision.Approved;
                stage.FinalHoldComment = comment;
                stage.FinalHoldDecidedAt = now;
            }
            else
            {
                stagesStartingNewRound.Add(stage);
            }
        }

        await SnapshotPhaseRoundIfNeededAsync(
            process, ApprovalStagePhase.FinalHold, process.FinalHoldStartedAt, null, stagesStartingNewRound);

        foreach (var stage in stagesStartingNewRound)
        {
            // Новый круг финальной выдержки для этого этапа - вложения/цитаты предыдущего
            // круга (Phase здесь не различает круги) иначе остались бы привязаны к той же
            // фазе FinalHold и отображались бы рядом с текстом решения, которое согласующий
            // ещё не принял.
            await ClearPreviousRoundArtifactsAsync(stage, ApprovalStagePhase.FinalHold);

            stage.FinalHoldDecision = ApprovalStageDecision.Pending;
            stage.FinalHoldComment = null;
            stage.FinalHoldDecidedAt = null;
        }
    }

    /// <summary>Удаляет ФАЙЛЫ, приложенные к решению этапа в предыдущем круге указанной фазы
    /// (Repeat/FinalHold), перед тем как круг перезапускается для этого этапа -
    /// VndApprovalStageAttachment различает только Phase, без номера круга внутри неё, а сами
    /// файлы занимают место в хранилище, поэтому предыдущий круг физически удаляется (текст
    /// решения при этом остаётся доступен через VndApprovalPhaseRound - см.
    /// SnapshotPhaseRoundIfNeededAsync).
    ///
    /// ЦИТАТЫ (VndApprovalStageQuote) в отличие от вложений теперь НЕ удаляются - каждая цитата
    /// с версии 20260918 несёт RevisionIndex (версию документа, к которой относится), и
    /// "живые" поля ответа (Primary/Repeat/FinalHoldQuotes) отфильтровываются по нему на
    /// уровне ToQuoteResponses (см. LoadResponseAsync) - старые цитаты сами перестают попадать
    /// в них, когда документ обновляется, без физического удаления. Раньше цитаты удалялись
    /// вместе с вложениями - из-за этого при просмотре прошлой версии документа ("Р1.1" и т.п.)
    /// её собственные замечания было решительно невозможно показать, они были уже стёрты (см.
    /// ApprovalProcessResponse.AllQuotes - именно ради этого цитаты теперь хранятся бессрочно,
    /// как и было изначально задумано в самом их док-комментарии).</summary>
    private async Task ClearPreviousRoundArtifactsAsync(VndApprovalStage stage, ApprovalStagePhase phase)
    {
        var oldAttachments = await _db.Set<VndApprovalStageAttachment>()
            .Where(a => a.VndApprovalStageId == stage.Id && a.Phase == phase)
            .ToListAsync();

        foreach (var old in oldAttachments)
        {
            try
            {
                await _fileService.DeleteAsync(old.FileAttachmentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Не удалось удалить файл {FileId} предыдущего круга согласования этапа {StageId}",
                    old.FileAttachmentId, stage.Id);
            }
        }
        _db.Set<VndApprovalStageAttachment>().RemoveRange(oldAttachments);
    }

    /// <summary>Снимает "фотографию" круга фазы Repeat/FinalHold прямо перед тем, как его
    /// данные будут перезаписаны следующим кругом (см. <see cref="VndApprovalPhaseRound"/> и
    /// вызовы этого метода в ResubmitAfterRevisionAsync/ResetFinalHoldDecisionsAsync выше).
    ///
    /// Ничего не создаёт (тихо выходит), если сохранять нечего - это самый первый заход в фазу
    /// (ни у одного этапа ещё нет решения по ней, и комментария инициатора тоже нет): в этом
    /// случае "предыдущего круга" попросту не было.</summary>
    private async Task SnapshotPhaseRoundIfNeededAsync(
        VndApprovalProcess process, ApprovalStagePhase phase, DateTime? startedAt,
        string? initiatorComment, IEnumerable<VndApprovalStage> stagesInPhase)
    {
        var decidedStages = stagesInPhase
            .Select(s => new
            {
                Stage = s,
                Decision = GetPhaseDecision(s, phase),
                Comment = GetPhaseComment(s, phase),
                DecidedAt = GetPhaseDecidedAt(s, phase),
            })
            .Where(x => x.Decision is not null && x.Decision != ApprovalStageDecision.Pending)
            .ToList();

        if (decidedStages.Count == 0 && string.IsNullOrEmpty(initiatorComment))
            return;

        var roundNumber = await _db.Set<VndApprovalPhaseRound>()
            .Where(r => r.ApprovalProcessId == process.Id && r.Phase == phase)
            .CountAsync() + 1;

        var round = new VndApprovalPhaseRound
        {
            ApprovalProcessId = process.Id,
            Phase = phase,
            RoundNumber = roundNumber,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            // У финальной выдержки отдельного комментария инициатора нет - RepeatInitiatorComment
            // относится только к фазе Repeat.
            InitiatorComment = phase == ApprovalStagePhase.Repeat ? initiatorComment : null,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var x in decidedStages)
        {
            round.StageDecisions.Add(new VndApprovalPhaseRoundStageDecision
            {
                VndApprovalStageId = x.Stage.Id,
                Decision = x.Decision!.Value,
                Comment = x.Comment,
                DecidedAt = x.DecidedAt,
            });
        }

        _db.Set<VndApprovalPhaseRound>().Add(round);
    }

    /// <summary>Определяет фазу/круг, чьи замечания привели к текущей повторной отправке — то
    /// есть круг, который эта отправка завершает (см. вызов в ResubmitAfterRevisionAsync,
    /// используется для VndRedactionRevisionSnapshot.Phase/RoundNumber). FinalHoldStartedAt
    /// проверяем первым: если он уже задан, процесс не мог вернуться в доработку из фазы
    /// Repeat — RevisionNeeded выставляется только в CompletePrimaryPhaseAsync,
    /// CompleteRepeatPhaseAsync и ReturnToRevisionFromFinalHoldAsync, и последняя срабатывает
    /// только когда финальная выдержка уже идёт. RoundNumber считается тем же способом, что и
    /// в SnapshotPhaseRoundIfNeededAsync выше (количество уже сохранённых кругов этой фазы +
    /// 1) — он совпадёт с номером круга, который SnapshotPhaseRoundIfNeededAsync создаст этим
    /// же вызовом ResubmitAfterRevisionAsync, если замечания устранены полностью.</summary>
    private async Task<(ApprovalStagePhase Phase, int? RoundNumber)> DetermineActiveRevisionPhaseAsync(
        VndApprovalProcess process)
    {
        if (process.FinalHoldStartedAt is not null)
        {
            var roundNumber = await _db.Set<VndApprovalPhaseRound>()
                .Where(r => r.ApprovalProcessId == process.Id && r.Phase == ApprovalStagePhase.FinalHold)
                .CountAsync() + 1;
            return (ApprovalStagePhase.FinalHold, roundNumber);
        }

        if (process.RepeatStartedAt is not null)
        {
            var roundNumber = await _db.Set<VndApprovalPhaseRound>()
                .Where(r => r.ApprovalProcessId == process.Id && r.Phase == ApprovalStagePhase.Repeat)
                .CountAsync() + 1;
            return (ApprovalStagePhase.Repeat, roundNumber);
        }

        return (ApprovalStagePhase.Primary, null);
    }

    private static ApprovalStageDecision? GetPhaseDecision(VndApprovalStage stage, ApprovalStagePhase phase) =>
        phase switch
        {
            ApprovalStagePhase.Primary => stage.PrimaryDecision,
            ApprovalStagePhase.Repeat => stage.RepeatDecision,
            ApprovalStagePhase.FinalHold => stage.FinalHoldDecision,
            _ => null,
        };

    private static string? GetPhaseComment(VndApprovalStage stage, ApprovalStagePhase phase) =>
        phase switch
        {
            ApprovalStagePhase.Primary => stage.PrimaryComment,
            ApprovalStagePhase.Repeat => stage.RepeatComment,
            ApprovalStagePhase.FinalHold => stage.FinalHoldComment,
            _ => null,
        };

    private static DateTime? GetPhaseDecidedAt(VndApprovalStage stage, ApprovalStagePhase phase) =>
        phase switch
        {
            ApprovalStagePhase.Primary => stage.PrimaryDecidedAt,
            ApprovalStagePhase.Repeat => stage.RepeatDecidedAt,
            ApprovalStagePhase.FinalHold => stage.FinalHoldDecidedAt,
            _ => null,
        };

    /// <summary>Сохраняет файлы, приложенные согласующим к резолюции конкретной фазы, и
    /// связывает их с этапом. Вызывается из DecideAsync до SaveChangesAsync — вложения
    /// остаются в истории согласования бессрочно, даже после того как редакция станет
    /// согласованной.</summary>
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

    /// <summary>Разбирает QuotesJson из ApprovalDecisionRequest. Невалидный JSON или отсутствие
    /// поля — не ошибка запроса (цитаты необязательны), просто пустой список.</summary>
    private static List<ApprovalQuoteItem> ParseQuotes(string? quotesJson)
    {
        if (string.IsNullOrWhiteSpace(quotesJson)) return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<ApprovalQuoteItem>>(
                quotesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return parsed ?? [];
        }
        catch (JsonException)
        {
            // Не роняем всю резолюцию из-за кривого QuotesJson - просто игнорируем цитаты.
            return [];
        }
    }

    /// <summary>Сохраняет цитаты, на которые согласующий сослался в резолюции конкретной фазы
    /// (см. ParseQuotes выше), и связывает их с этапом и версией документа, к которой они
    /// относятся (revisionIndex - см. VndApprovalStageQuote.RevisionIndex). Вызывается из
    /// DecideAsync до SaveChangesAsync - см. AttachDecisionFilesAsync выше, тот же паттерн.</summary>
    private void AttachDecisionQuotes(
        VndApprovalStage stage, ApprovalStagePhase phase, List<ApprovalQuoteItem> quotes, int revisionIndex)
    {
        if (quotes.Count == 0) return;

        foreach (var quote in quotes.Take(MaxQuotesPerDecision))
        {
            var text = quote.Text.Trim();
            if (text.Length == 0) continue;
            if (text.Length > MaxQuoteTextLength) text = text[..MaxQuoteTextLength];

            _db.Set<VndApprovalStageQuote>().Add(new VndApprovalStageQuote
            {
                VndApprovalStageId = stage.Id,
                Phase = phase,
                DocumentTarget = quote.DocumentTarget,
                Text = text,
                RevisionIndex = revisionIndex,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>Обязательные этапы маршрута больше не завязаны на фиксированный enum/позиции -
    /// они определяются динамическим справочником CoordinationDefaultApprover (dictionaries/
    /// coordination-users), в порядке его поля Order. Ведущие N этапов запроса (N = число
    /// активных записей справочника) обязаны 1-в-1 соответствовать этим записям в том же
    /// порядке; всё, что идёт после - произвольные (Custom) этапы, добавленные инициатором.</summary>
    private async Task<List<VndApprovalStage>> BuildAndValidateStagesAsync(List<ApprovalStageRequest> requestStages)
    {
        var fixedCatalog = await _db.Set<CoordinationDefaultApprover>()
            .OrderBy(x => x.Order)
            .ToListAsync();

        if (requestStages.Count < fixedCatalog.Count)
            throw new InvalidOperationException(
                "Маршрут должен содержать все обязательные этапы: " +
                string.Join(", ", fixedCatalog.Select(x => x.Title)));

        for (var i = 0; i < fixedCatalog.Count; i++)
        {
            if (requestStages[i].CoordinationStageId != fixedCatalog[i].Id)
                throw new InvalidOperationException(
                    $"Этап {i + 1} маршрута всегда — «{fixedCatalog[i].Title}»");
        }

        for (var i = fixedCatalog.Count; i < requestStages.Count; i++)
        {
            if (requestStages[i].CoordinationStageId is not null)
                throw new InvalidOperationException(
                    "Этапы после обязательных должны быть произвольными (без ссылки на справочник)");
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
        var catalogById = fixedCatalog.ToDictionary(x => x.Id);

        var stages = new List<VndApprovalStage>();
        for (var i = 0; i < requestStages.Count; i++)
        {
            var reqStage = requestStages[i];
            var approver = usersById[reqStage.ApproverUserId];

            var catalogEntry = reqStage.CoordinationStageId.HasValue
                ? catalogById[reqStage.CoordinationStageId.Value]
                : null;
            var expectedOrgUnitId = catalogEntry?.OrgUnitId;

            if (expectedOrgUnitId.HasValue && approver.OrgUnitId != expectedOrgUnitId)
                throw new InvalidOperationException(
                    $"Согласующий на этапе {i + 1} ({catalogEntry!.Title}) должен относиться к соответствующему подразделению");

            stages.Add(new VndApprovalStage
            {
                Order = i + 1,
                Kind = catalogEntry is not null ? ApprovalStageKind.Fixed : ApprovalStageKind.Custom,
                Title = catalogEntry?.Title ?? "Доп. согласующий",
                CoordinationStageId = catalogEntry?.Id,
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
                   .Include(x => x.Stages).ThenInclude(s => s.Quotes)
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
            .Include(x => x.Stages).ThenInclude(s => s.Quotes)
            .Include(x => x.DisagreementMatrixRows)
            .Include(x => x.RepeatInitiatorCommentAttachments).ThenInclude(a => a.FileAttachment)
            .Include(x => x.PhaseRounds).ThenInclude(r => r.StageDecisions)
            .Include(x => x.RedactionSnapshots).ThenInclude(s => s.DocFileRu)
            .Include(x => x.RedactionSnapshots).ThenInclude(s => s.DocFileKg)
            .Include(x => x.RedactionSnapshots).ThenInclude(s => s.DocFileEn)
            .Include(x => x.RedactionSnapshots).ThenInclude(s => s.TidFile)
            .Include(x => x.RedactionSnapshots).ThenInclude(s => s.DisagreementMatrixFile)
            .FirstAsync(x => x.Id == processId);

        var initiator = await _db.Users
            .Include(u => u.Position)
            .FirstOrDefaultAsync(u => u.Id == process.InitiatorUserId);

        // Версия документа редакции, которая сейчас живая (RedactionSnapshots уже загружены
        // выше через Include) - см. VndApprovalStageQuote.RevisionIndex. "Живые" поля
        // Primary/Repeat/FinalHoldQuotes ниже показывают только цитаты именно этой версии.
        var liveRevisionIndex = process.RedactionSnapshots.Count;

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
            RepeatInitiatorCommentAttachments = process.RepeatInitiatorCommentAttachments
                .OrderBy(a => a.CreatedAt)
                .Select(a => new ApprovalStageAttachmentResponse
                {
                    Id = a.Id,
                    FileId = a.FileAttachmentId,
                    FileName = a.FileAttachment?.OriginalFileName ?? "",
                    SizeBytes = a.FileAttachment?.SizeBytes ?? 0
                })
                .ToList(),
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
            PhaseRounds = process.PhaseRounds
                .OrderBy(r => r.Phase)
                .ThenBy(r => r.RoundNumber)
                .Select(r => new ApprovalPhaseRoundResponse
                {
                    Id = r.Id,
                    Phase = MapPhase(r.Phase),
                    RoundNumber = r.RoundNumber,
                    StartedAt = r.StartedAt,
                    CompletedAt = r.CompletedAt,
                    InitiatorComment = r.InitiatorComment,
                    StageDecisions = r.StageDecisions.Select(d => new ApprovalPhaseRoundStageDecisionResponse
                    {
                        StageId = d.VndApprovalStageId,
                        Decision = MapDecision(d.Decision),
                        Comment = d.Comment,
                        DecidedAt = d.DecidedAt,
                    }).ToList(),
                })
                .ToList(),
            Stages = process.Stages.OrderBy(s => s.Order).Select(s => new ApprovalStageResponse
            {
                Id = s.Id,
                Order = s.Order,
                Kind = MapKind(s.Kind),
                Title = s.Title ?? LegacyKindTitle(s.Kind),
                OrgUnitId = s.OrgUnitId,
                OrgUnitName = s.OrgUnit?.TitleRu ?? "",
                ApproverUserId = s.ApproverUserId,
                ApproverName = s.ApproverUser?.FullName ?? "",
                IsRemovedByEditor = s.IsRemovedByEditor,
                PrimaryDecision = MapDecision(s.PrimaryDecision),
                PrimaryComment = s.PrimaryComment,
                PrimaryDecidedAt = s.PrimaryDecidedAt,
                PrimaryAttachments = ToAttachmentResponses(s.Attachments, ApprovalStagePhase.Primary),
                PrimaryQuotes = ToQuoteResponses(s.Quotes, ApprovalStagePhase.Primary, liveRevisionIndex),
                ParticipatesInRepeat = s.ParticipatesInRepeat,
                RepeatDecision = s.RepeatDecision.HasValue ? MapDecision(s.RepeatDecision.Value) : null,
                RepeatComment = s.RepeatComment,
                RepeatDecidedAt = s.RepeatDecidedAt,
                RepeatAttachments = ToAttachmentResponses(s.Attachments, ApprovalStagePhase.Repeat),
                RepeatQuotes = ToQuoteResponses(s.Quotes, ApprovalStagePhase.Repeat, liveRevisionIndex),
                FinalHoldDecision = s.FinalHoldDecision.HasValue ? MapDecision(s.FinalHoldDecision.Value) : null,
                FinalHoldComment = s.FinalHoldComment,
                FinalHoldDecidedAt = s.FinalHoldDecidedAt,
                FinalHoldAttachments = ToAttachmentResponses(s.Attachments, ApprovalStagePhase.FinalHold),
                FinalHoldQuotes = ToQuoteResponses(s.Quotes, ApprovalStagePhase.FinalHold, liveRevisionIndex)
            }).ToList(),
            AllQuotes = process.Stages
                .SelectMany(s => s.Quotes)
                .OrderBy(q => q.CreatedAt)
                .Select(ToQuoteResponse)
                .ToList(),
            RedactionSnapshots = process.RedactionSnapshots
                .OrderBy(s => s.SnapshotNumber)
                .Select(s => new VndRedactionRevisionSnapshotResponse
                {
                    Id = s.Id,
                    SnapshotNumber = s.SnapshotNumber,
                    Phase = MapSnapshotPhase(s.Phase),
                    RoundNumber = s.RoundNumber,
                    DocFileRuId = s.DocFileRuId,
                    DocFileRuName = s.DocFileRu?.OriginalFileName,
                    DocFileKgId = s.DocFileKgId,
                    DocFileKgName = s.DocFileKg?.OriginalFileName,
                    DocFileEnId = s.DocFileEnId,
                    DocFileEnName = s.DocFileEn?.OriginalFileName,
                    TidFileId = s.TidFileId,
                    TidFileName = s.TidFile?.OriginalFileName,
                    DisagreementMatrixFileId = s.DisagreementMatrixFileId,
                    DisagreementMatrixFileName = s.DisagreementMatrixFile?.OriginalFileName,
                    CreatedAt = s.CreatedAt,
                })
                .ToList(),
        };
    }

    private static DisagreementMatrixRowResponse ToDisagreementRowResponse(VndDisagreementMatrixRow row) => new()
    {
        Id = row.Id,
        DeveloperPosition = row.DeveloperPosition,
        OpponentPosition = row.OpponentPosition,
        DeveloperJustification = row.DeveloperJustification,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt
    };

    /// <summary>Вложения этапа для конкретной фазы решения (первичной/повторной/финальной).
    /// Остаются доступны и после согласования редакции — часть истории согласования наравне
    /// с текстом резолюции.</summary>
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

    /// <summary>Цитаты этапа для конкретной фазы решения, ТОЛЬКО относящиеся к текущей живой
    /// версии документа (liveRevisionIndex - см. VndApprovalStageQuote.RevisionIndex) — см.
    /// ToAttachmentResponses выше, тот же паттерн, плюс фильтр по версии. Цитаты прошлых версий
    /// сюда не попадают (иначе замечания к уже исправленной версии "приклеивались" бы к новой),
    /// но по-прежнему доступны клиенту целиком через ApprovalProcessResponse.AllQuotes.</summary>
    private static List<ApprovalStageQuoteResponse> ToQuoteResponses(
        IEnumerable<VndApprovalStageQuote> quotes, ApprovalStagePhase phase, int liveRevisionIndex) =>
        quotes
            .Where(q => q.Phase == phase && q.RevisionIndex == liveRevisionIndex)
            .OrderBy(q => q.CreatedAt)
            .Select(ToQuoteResponse)
            .ToList();

    private static ApprovalStageQuoteResponse ToQuoteResponse(VndApprovalStageQuote q) => new()
    {
        Id = q.Id,
        StageId = q.VndApprovalStageId,
        Phase = MapSnapshotPhase(q.Phase),
        DocumentTarget = q.DocumentTarget,
        Text = q.Text,
        RevisionIndex = q.RevisionIndex,
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
        ApprovalStageKind.Fixed => "fixed",
        _ => "custom"
    };

    /// <summary>Название этапа для маршрутов, построенных ДО перехода на динамический
    /// справочник (VndApprovalStage.Title тогда ещё не заполнялся) - только для отображения
    /// старой истории согласования, в новой логике не используется.</summary>
    private static string LegacyKindTitle(ApprovalStageKind kind) => kind switch
    {
        ApprovalStageKind.Legal => "Юридическое управление",
        ApprovalStageKind.RiskManagement => "Риск-менеджмент",
        ApprovalStageKind.Compliance => "Комплаенс-контроль",
        ApprovalStageKind.Methodology => "Методология",
        _ => "Доп. согласующий"
    };

    private static string MapDecision(ApprovalStageDecision decision) => decision switch
    {
        ApprovalStageDecision.Pending => "pending",
        ApprovalStageDecision.Approved => "approved",
        ApprovalStageDecision.ApprovedWithComment => "approved_with_comment",
        ApprovalStageDecision.Rejected => "rejected",
        ApprovalStageDecision.AutoApprovedByTimeout => "auto_approved_timeout",
        ApprovalStageDecision.RemovedByEditor => "removed_by_editor",
        _ => "pending"
    };

    /// <summary>"repeat"/"finalHold" - см. ApprovalPhaseRoundResponse.Phase на клиенте
    /// (ApprovalPhase в coordinationServiceTypes.ts). Primary сюда не попадает - у него
    /// снимков круга не бывает, см. VndApprovalPhaseRound.</summary>
    private static string MapPhase(ApprovalStagePhase phase) => phase switch
    {
        ApprovalStagePhase.Repeat => "repeat",
        ApprovalStagePhase.FinalHold => "finalHold",
        _ => "repeat"
    };

    /// <summary>"primary"/"repeat"/"finalHold" — в отличие от MapPhase выше (только для
    /// VndApprovalPhaseRound, где Primary не бывает), снимок файлов редакции может относиться
    /// и к первичному согласованию — см. VndRedactionRevisionSnapshot.Phase.</summary>
    private static string MapSnapshotPhase(ApprovalStagePhase phase) => phase switch
    {
        ApprovalStagePhase.Primary => "primary",
        ApprovalStagePhase.Repeat => "repeat",
        ApprovalStagePhase.FinalHold => "finalHold",
        _ => "primary"
    };
}