using delosfera_server.Common.Services.Authorization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.ActivityLog.Models;
using delosfera_server.Modules.ActivityLog.Services;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Documents.VND.Messages;
using ActivityText = delosfera_server.Modules.ActivityLog.Models.ActivityText;

namespace delosfera_server.Modules.Documents.VND.Services;

public class VndActualizationService : IVndActualizationService
{
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILogger<VndActualizationService> _logger;
    private readonly IActivityLogService _activityLog;

    public VndActualizationService(
        DelosferaDbContext db,
        ICurrentUserService currentUser,
        INotificationService notifications,
        ILogger<VndActualizationService> logger,
        IActivityLogService activityLog
    )
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _logger = logger;
        _activityLog = activityLog;
    }

    /// <summary>Единое определение "главный редактор" для всей актуализации — умышленно шире,
    /// чем просто ActualizeAnyVnd(With/Without)Approval: пользователь с правом создавать ВНД
    /// (CreateVndWithApproval/CreateVndWithoutApproval) тоже действует как главный редактор
    /// (см. VndService.IsChiefEditor/VndApprovalService.IsChiefEditor — тот же набор прав).
    /// </summary>
    private bool IsChiefEditor() =>
        _currentUser.HasPermission(PermissionCode.CreateVndWithApproval)
        || _currentUser.HasPermission(PermissionCode.CreateVndWithoutApproval)
        || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval)
        || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);

    /// <summary>Шаг А (для главного редактора, прямой старт без заявки) — только переводит
    /// документ в "На актуализации" и фиксирует ответственного/порядок согласования. Сдвиг срока
    /// и "актуализация без изменений" здесь сознательно НЕ запрашиваются — они решаются позже,
    /// отдельным шагом "Выполнить актуализацию" (см. PerformAsync), который может выполнить как
    /// сам главный редактор, так и назначенный им ответственный.</summary>
    public async Task<VndActualizationStateResponse> StartAsync(
        int vndId, StartActualizationRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.Active)
            throw new InvalidOperationException("Начать актуализацию можно только для действующего ВНД");

        var canWithoutApproval = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);
        var canWithApproval = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval);

        if (!canWithoutApproval && !canWithApproval)
            throw new UnauthorizedAccessException(
                "У вас нет права брать любую ВНД в актуализацию. Используйте запрос доступа");

        if (!request.RequiresApproval && !canWithoutApproval)
            throw new UnauthorizedAccessException(
                "У вас нет права актуализировать без согласования — выберите вариант \"с согласованием\"");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        var responsibleUserId = request.ResponsibleUserId ?? currentUserId;
        var responsibleExists = await _db.Users.AnyAsync(x => x.Id == responsibleUserId);
        if (!responsibleExists)
            throw new KeyNotFoundException($"Пользователь с id={responsibleUserId} не найден");

        vnd.Status = VndStatus.OnActualization;
        vnd.ActualizationResponsibleUserId = responsibleUserId;
        vnd.ActualizationRequiresApproval = request.RequiresApproval;
        // Сдвиг срока и "без изменений" — ещё не решены, это шаг "Выполнить актуализацию"
        vnd.ActualizationShiftNextPeriod = false;
        vnd.ActualizationPlannedNoChanges = false;
        vnd.ActualizationPerformed = false;

        // --- Открываем запись в истории циклов актуализации
        _db.Set<VndActualizationRecord>().Add(new VndActualizationRecord
        {
            VndId = vndId,
            ResponsibleUserId = responsibleUserId,
            RequiresApproval = request.RequiresApproval,
            ShiftNextPeriod = false,
            PlannedNoChanges = false,
            StartedAt = DateTime.UtcNow,
            DueActualizationDateBefore = vnd.DueActualizationDate
        });

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ProcessStarted, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} взял(а) в актуализацию ВНД «{vnd.TitleRu}»",
                $"{actorName} took VND \"{vnd.TitleRu}\" for actualization",
                $"{actorName} «{vnd.TitleRu}» ВНДисин актуалдаштырууга алды"),
            $"/base-vnd/{vndId}");

        // --- Закрываем все pending-заявки на доступ к актуализации этого ВНД: раз актуализация
        // стартовала напрямую (главным редактором/админом), решать по этим заявкам уже нечего —
        // либо заявитель сам стал ответственным (Approved), либо ответственным назначен кто-то
        // другой (Rejected), заявителю в этом случае приходит уведомление с пояснением.
        var pendingRequests = await _db.VndActualizationRequests
            .Where(x => x.VndId == vndId && x.Status == ActualizationAccessStatus.Pending)
            .ToListAsync();

        foreach (var pending in pendingRequests)
        {
            var becomesResponsible = pending.RequestedByUserId == responsibleUserId;
            pending.Status = becomesResponsible
                ? ActualizationAccessStatus.Approved
                : ActualizationAccessStatus.Rejected;
            pending.DecidedByUserId = currentUserId;
            pending.DecidedAt = DateTime.UtcNow;

            // Заявка сразу используется этим же стартом (заявитель становится ответственным
            // прямо сейчас) — помечаем её потраченной, чтобы она не "ожила" в следующем цикле.
            if (becomesResponsible)
                pending.ConsumedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // --- Уведомления заявителям о закрытых заявках
        foreach (var pending in pendingRequests)
        {
            var notice = pending.Status == ActualizationAccessStatus.Approved
                ? VndActualizationNotificationMessages.AccessApproved(vnd.TitleRu)
                : VndActualizationNotificationMessages.AccessRejectedDirectStart(vnd.TitleRu,
                    responsibleUserId == currentUserId);

            await NotifyAsync(notice, vndId, currentUserId, [pending.RequestedByUserId]);
        }

        // --- Если ответственным назначен не сам инициировавший старт, а другой пользователь —
        // тот должен узнать, что ему нужно выполнить шаг "Выполнить актуализацию" 
        if (responsibleUserId != currentUserId)
        {
            await NotifyAsync(
                VndActualizationNotificationMessages.AssignedResponsible(vnd.TitleRu, actorName),
                vndId, currentUserId, [responsibleUserId], urlOverride: $"/base-vnd/{vndId}?tab=actual");
        }

        return await BuildStateResponseAsync(vnd);
    }

    /// <summary>Шаг Б для цикла, начатого напрямую главным редактором (StartAsync) — фиксирует
    /// финальные "сдвигать ли срок" и "актуализация без изменений". До этого шага загрузка новой
    /// редакции заблокирована (см. VndService.AddRedactionAsync, проверка ActualizationPerformed).
    /// Доступен как самому назначившему себя главному редактору, так и любому другому назначенному
    /// им ответственному, а также любому главному редактору (может выполнить его за ответственного).
    /// Для пути "по заявке" этот шаг совмещён со стартом — см. ConfirmStartAfterRequestAsync,
    /// вызывать PerformAsync после него не нужно (и нельзя — ActualizationPerformed уже true).</summary>
    public async Task<VndActualizationStateResponse> PerformAsync(
        int vndId, PerformActualizationRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.OnActualization)
            throw new InvalidOperationException("Выполнить актуализацию можно только в процессе актуализации");

        if (vnd.ActualizationPerformed)
            throw new InvalidOperationException("Шаг «Выполнить актуализацию» для этого цикла уже пройден");

        if (vnd.ActualizationResponsibleUserId != currentUserId && !IsChiefEditor())
            throw new UnauthorizedAccessException(
                "Выполнить актуализацию может только назначенный ответственный или главный редактор ВНД");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        vnd.ActualizationShiftNextPeriod = request.ShiftNextPeriod;
        vnd.ActualizationPlannedNoChanges = request.PlannedNoChanges;
        vnd.ActualizationPerformed = true;

        var openRecord = await _db.Set<VndActualizationRecord>()
            .Where(r => r.VndId == vndId && r.PublishedAt == null)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (openRecord is not null)
        {
            openRecord.ShiftNextPeriod = request.ShiftNextPeriod;
            openRecord.PlannedNoChanges = request.PlannedNoChanges;
            openRecord.PerformedAt = DateTime.UtcNow;
        }

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ProcessStarted, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} выполнил(а) шаг «Выполнить актуализацию» ВНД «{vnd.TitleRu}»" +
                (request.PlannedNoChanges ? " (заявлено без изменений)" : ""),
                $"{actorName} completed the \"Perform actualization\" step for VND \"{vnd.TitleRu}\"" +
                (request.PlannedNoChanges ? " (declared as no changes)" : ""),
                $"{actorName} «{vnd.TitleRu}» ВНДисинин «Актуализацияны аткаруу» кадамын аткарды" +
                (request.PlannedNoChanges ? " (өзгөртүүсүз деп жарыяланды)" : "")),
            $"/base-vnd/{vndId}");

        await _db.SaveChangesAsync();

        return await BuildStateResponseAsync(vnd);
    }

    public async Task<VndActualizationRequestResponse> RequestAccessAsync(
        int vndId, RequestActualizationAccessRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.Active)
            throw new InvalidOperationException(
                "Запросить доступ к актуализации можно только для действующего ВНД");

        var requiredPermission = request.RequiresApproval
            ? PermissionCode.ActualizeVndWithApprovalByRequest
            : PermissionCode.ActualizeVndWithoutApprovalByRequest;

        if (!_currentUser.HasPermission(requiredPermission))
            throw new UnauthorizedAccessException(
                $"У вас нет права \"{(request.RequiresApproval ? "с последующим согласованием" : "без согласования")}\" (по запросу)");

        var alreadyPending = await _db.VndActualizationRequests.AnyAsync(x =>
            x.VndId == vndId && x.RequestedByUserId == currentUserId
                             && x.Status == ActualizationAccessStatus.Pending);
        if (alreadyPending)
            throw new InvalidOperationException("У вас уже есть заявка по этому ВНД, ожидающая решения");

        var entity = new VndActualizationRequest
        {
            VndId = vndId,
            RequestedByUserId = currentUserId,
            RequiresApproval = request.RequiresApproval,
            ShiftNextPeriod = request.ShiftNextPeriod,
            Status = ActualizationAccessStatus.Pending
        };

        _db.VndActualizationRequests.Add(entity);
        await _db.SaveChangesAsync();

        var requester = await _db.Users.FindAsync(currentUserId);
        var chiefEditorIds = await GetChiefEditorIdsAsync();

        // Ведём прямо на вкладку «Актуализация» этого документа — там сразу видно, кто
        // запросил доступ, и можно одобрить в один клик (не через реквизиты/маршрут вручную).
        await NotifyAsync(
            VndActualizationNotificationMessages.AccessRequested(vnd.TitleRu, requester?.FullName ?? "—"),
            vndId, currentUserId, chiefEditorIds.ToArray(), urlOverride: $"/base-vnd/{vndId}?tab=actual");

        return await LoadRequestResponseAsync(entity.Id);
    }

    public async Task<List<VndActualizationRequestResponse>> GetPendingRequestsAsync(int currentUserId)
    {
        var canWithoutApproval = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);
        var canWithApproval = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval);
        if (!canWithoutApproval && !canWithApproval)
            throw new UnauthorizedAccessException("Просматривать заявки может только главный редактор ВНД");

        var requests = await _db.VndActualizationRequests
            .Include(x => x.Vnd)
            .Include(x => x.RequestedByUser)
            .Include(x => x.DecidedByUser)
            .Where(x => x.Status == ActualizationAccessStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        return requests.Select(ToRequestResponse).ToList();
    }

    /// <summary>Решение по заявке — approve/reject. При одобрении главный редактор может
    /// скорректировать пожелание заявителя насчёт сдвига срока (тогда заявителю отдельно
    /// уходит уведомление об этом). Одновременно все ОСТАЛЬНЫЕ pending-заявки по этому же ВНД
    /// автоматически отклоняются.</summary>
    public async Task<VndActualizationRequestResponse> DecideRequestAsync(
        int requestId, ActualizationRequestDecisionRequest request, int currentUserId)
    {
        var canWithoutApproval = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);
        var canWithApproval = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval);
        if (!canWithoutApproval && !canWithApproval)
            throw new UnauthorizedAccessException("Решения по заявкам принимает только главный редактор ВНД");

        var current = await _db.VndActualizationRequests
                          .Include(x => x.Vnd)
                          .FirstOrDefaultAsync(x => x.Id == requestId)
                      ?? throw new KeyNotFoundException($"Заявка с id={requestId} не найдена");

        if (current.Status != ActualizationAccessStatus.Pending)
            throw new InvalidOperationException("Решение по этой заявке уже принято");

        if (request.Approve && request.ShiftNextPeriod is null)
            throw new InvalidOperationException(
                "При одобрении заявки нужно указать, сдвигать ли срок следующей актуализации");

        var requestedShift = current.ShiftNextPeriod; // исходное пожелание заявителя
        var shiftOverridden = request.Approve
                               && request.ShiftNextPeriod!.Value != requestedShift;

        current.Status = request.Approve ? ActualizationAccessStatus.Approved : ActualizationAccessStatus.Rejected;
        current.DecidedByUserId = currentUserId;
        current.DecidedAt = DateTime.UtcNow;

        if (request.Approve)
            current.ShiftNextPeriod = request.ShiftNextPeriod!.Value;

        // --- Остальные pending-заявки на актуализацию этого же ВНД: как только по одной из них
        // принято решение (даже отклонение), по остальным решать уже нечего только в случае
        // одобрения (появился ответственный) — иначе, если эту заявку просто отклонили, другие
        // заявки на общих основаниях остаются ожидать решения.
        var otherPending = request.Approve
            ? await _db.VndActualizationRequests
                .Where(x => x.VndId == current.VndId && x.Id != current.Id
                            && x.Status == ActualizationAccessStatus.Pending)
                .ToListAsync()
            : new List<VndActualizationRequest>();

        foreach (var other in otherPending)
        {
            other.Status = ActualizationAccessStatus.Rejected;
            other.DecidedByUserId = currentUserId;
            other.DecidedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var vndTitle = current.Vnd!.TitleRu;

        var notice = request.Approve
            ? (shiftOverridden
                ? VndActualizationNotificationMessages.AccessApprovedShiftOverridden(vndTitle, request.ShiftNextPeriod!.Value)
                : VndActualizationNotificationMessages.AccessApproved(vndTitle))
            : VndActualizationNotificationMessages.AccessRejected(vndTitle);

        await NotifyAsync(notice, current.VndId, currentUserId, [current.RequestedByUserId]);

        foreach (var other in otherPending)
        {
            await NotifyAsync(
                VndActualizationNotificationMessages.AccessRejectedAnotherApproved(vndTitle),
                current.VndId, currentUserId, [other.RequestedByUserId]);
        }

        return await LoadRequestResponseAsync(current.Id);
    }

    /// <summary>Шаг "Выполнить актуализацию" для пути "по заявке" (обычный редактор) — совмещает
    /// в себе и старт цикла (переход в "На актуализации"), и фиксацию финальных условий: этот
    /// путь, в отличие от прямого старта главным редактором, не разбит на два отдельных клика —
    /// пользователь видит единственную кнопку "Выполнить актуализацию" после того, как его
    /// заявка одобрена. ShiftNextPeriod берём из одобренной заявки (решённое значение — то, что
    /// осталось после возможной корректировки главным редактором в DecideRequestAsync), заново
    /// не запрашиваем.</summary>
    public async Task<VndActualizationStateResponse> ConfirmStartAfterRequestAsync(
        int vndId, ConfirmActualizationStartRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.Active)
            throw new InvalidOperationException("Начать актуализацию можно только для действующего ВНД");

        // Берём самую свежую одобренную и ещё не использованную заявку текущего пользователя по
        // этому ВНД. ConsumedAt == null отсекает заявки, уже потраченные на предыдущий цикл —
        // без этого фильтра одна и та же одобренная заявка могла бы бесконечно автостартовать
        // актуализацию в каждом следующем цикле, даже если её никто не выдавал заново.
        var approvedRequest = await _db.VndActualizationRequests
                                  .Where(x => x.VndId == vndId && x.RequestedByUserId == currentUserId
                                                               && x.Status == ActualizationAccessStatus.Approved
                                                               && x.ConsumedAt == null)
                                  .OrderByDescending(x => x.DecidedAt)
                                  .FirstOrDefaultAsync()
                              ?? throw new InvalidOperationException(
                                  "Нет одобренной и ещё не использованной заявки на актуализацию этого ВНД для текущего пользователя");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        vnd.Status = VndStatus.OnActualization;
        vnd.ActualizationResponsibleUserId = currentUserId;
        vnd.ActualizationRequiresApproval = approvedRequest.RequiresApproval;
        vnd.ActualizationShiftNextPeriod = approvedRequest.ShiftNextPeriod;
        vnd.ActualizationPlannedNoChanges = request.PlannedNoChanges;
        vnd.ActualizationPerformed = true;

        approvedRequest.ConsumedAt = DateTime.UtcNow;

        var now = DateTime.UtcNow;

        // --- Открываем запись в истории циклов актуализации — сразу с выполненным шагом
        // "Выполнить актуализацию" (для этого пути старт и выполнение совмещены)
        _db.Set<VndActualizationRecord>().Add(new VndActualizationRecord
        {
            VndId = vndId,
            ResponsibleUserId = currentUserId,
            RequiresApproval = approvedRequest.RequiresApproval,
            ShiftNextPeriod = approvedRequest.ShiftNextPeriod,
            PlannedNoChanges = request.PlannedNoChanges,
            StartedAt = now,
            PerformedAt = now,
            DueActualizationDateBefore = vnd.DueActualizationDate
        });

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ProcessStarted, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} начал(а) актуализацию ВНД «{vnd.TitleRu}» по одобренной заявке" +
                (request.PlannedNoChanges ? " (заявлено без изменений)" : ""),
                $"{actorName} started actualization of VND \"{vnd.TitleRu}\" under an approved request" +
                (request.PlannedNoChanges ? " (declared as no changes)" : ""),
                $"{actorName} бекитилген арыз боюнча «{vnd.TitleRu}» ВНДисин актуалдаштырууну баштады" +
                (request.PlannedNoChanges ? " (өзгөртүүсүз деп жарыяланды)" : "")),
            $"/base-vnd/{vndId}");

        await _db.SaveChangesAsync();

        return await BuildStateResponseAsync(vnd);
    }

    /// <summary>Подтвердить, что заявленная "актуализация без изменений" (см.
    /// VndDocument.ActualizationPlannedNoChanges) действительно прошла без изменений и
    /// согласование не требовалось — документ сразу переходит в "Консолидация" без загрузки
    /// новой редакции. Если для цикла требуется согласование — используй вместо этого
    /// обычный запуск согласования (VndApprovalService.StartAsync), которое в этом случае
    /// разрешено запустить над уже действующей редакцией.</summary>
    public async Task<VndActualizationStateResponse> ConfirmNoChangesAsync(int vndId, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.OnActualization)
            throw new InvalidOperationException("Подтвердить отсутствие изменений можно только в процессе актуализации");

        if (!vnd.ActualizationPerformed)
            throw new InvalidOperationException(
                "Прежде выполните шаг «Выполнить актуализацию» и укажите, что актуализация без изменений");

        if (!vnd.ActualizationPlannedNoChanges)
            throw new InvalidOperationException("Для этого цикла не была заявлена актуализация без изменений");

        if (vnd.ActualizationRequiresApproval)
            throw new InvalidOperationException(
                "Для этого цикла требуется согласование — запустите его во вкладке «Согласование», " +
                "а не подтверждайте отсутствие изменений напрямую");

        if (vnd.ActualizationResponsibleUserId != currentUserId && !IsChiefEditor())
            throw new UnauthorizedAccessException(
                "Подтвердить отсутствие изменений может только ответственный за актуализацию или главный редактор ВНД");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        vnd.Status = VndStatus.Consolidation;

        var openRecord = await _db.Set<VndActualizationRecord>()
            .Where(r => r.VndId == vndId && r.PublishedAt == null)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (openRecord is not null && openRecord.ConsolidationStartedAt is null)
            openRecord.ConsolidationStartedAt = DateTime.UtcNow;

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Finalized, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} подтвердил(а) отсутствие изменений при актуализации ВНД «{vnd.TitleRu}», " +
                "ВНД переведён в статус «Консолидация»",
                $"{actorName} confirmed no changes while actualizing VND \"{vnd.TitleRu}\", " +
                "VND moved to \"Consolidation\" status",
                $"{actorName} «{vnd.TitleRu}» ВНДисин актуалдаштырууда өзгөртүүлөр жоктугун ырастады, " +
                "ВНД «Консолидация» абалына өттү"),
            $"/base-vnd/{vndId}");

        await _db.SaveChangesAsync();

        return await BuildStateResponseAsync(vnd);
    }

    public async Task<VndActualizationStateResponse> PublishAsync(
        int vndId, PublishVndActualizationRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments
                      .Include(x => x.Redactions)
                      .FirstOrDefaultAsync(x => x.Id == vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.Consolidation)
            throw new InvalidOperationException("Опубликовать можно только ВНД в статусе консолидации");

        var isChiefEditor = IsChiefEditor();

        bool isAuthorized;
        if (vnd.ActualizationResponsibleUserId.HasValue)
        {
            // Публикация в рамках цикла актуализации — только назначенный ответственный или главред
            isAuthorized = vnd.ActualizationResponsibleUserId == currentUserId || isChiefEditor;
        }
        else
        {
            // Обычное согласование (вне актуализации) — публикует инициатор согласования или главред
            var lastRedaction = vnd.Redactions.OrderByDescending(r => r.Number).FirstOrDefault();
            var initiatorUserId = lastRedaction is null
                ? (int?)null
                : await _db.VndApprovalProcesses
                    .Where(p => p.RedactionId == lastRedaction.Id)
                    .Select(p => (int?)p.InitiatorUserId)
                    .FirstOrDefaultAsync();

            isAuthorized = (initiatorUserId.HasValue && initiatorUserId == currentUserId) || isChiefEditor;
        }

        if (!isAuthorized)
            throw new UnauthorizedAccessException(
                "Опубликовать редакцию может только ответственный за актуализацию, инициатор согласования или главный редактор ВНД");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (vnd.ActualizationShiftNextPeriod)
        {
            vnd.DueActualizationDate = vnd.Period == ActualizationPeriod.Custom
                ? request.NewDueActualizationDate
                  ?? throw new InvalidOperationException(
                      "Для периода Custom укажите новую дату актуализации (NewDueActualizationDate)")
                : ResolveShiftedDueDate(vnd.Period, today);
        }
        // если ShiftNextPeriod == false — DueActualizationDate не трогаем

        var latestRedaction = vnd.Redactions.OrderByDescending(r => r.Number).FirstOrDefault();
        if (latestRedaction is not null)
            vnd.CurrentRedactionId = latestRedaction.Id;

        vnd.LastActualizationDate = today;
        vnd.LastActualizationHadChanges = request.HadChanges;
        vnd.RevisionChangedDate = today;
        vnd.Status = VndStatus.Active;

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Published, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} опубликовал(а) ВНД «{vnd.TitleRu}» после актуализации",
                $"{actorName} published VND \"{vnd.TitleRu}\" after actualization",
                $"{actorName} актуалдаштыруудан кийин «{vnd.TitleRu}» ВНДисин жарыялады"),
            $"/base-vnd/{vndId}");

        // --- Закрываем открытую запись истории (если публикация происходит в рамках цикла
        // актуализации — при обычном согласовании вне актуализации открытой записи нет,
        // и это ожидаемо: history здесь только про циклы актуализации).
        var openRecord = await _db.Set<VndActualizationRecord>()
            .Where(r => r.VndId == vndId && r.PublishedAt == null)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (openRecord is not null)
        {
            openRecord.PublishedAt = DateTime.UtcNow;
            openRecord.HadChanges = request.HadChanges;
            openRecord.DueActualizationDateAfter = vnd.DueActualizationDate;
        }

        vnd.ActualizationResponsibleUserId = null;
        vnd.ActualizationRequiresApproval = false;
        vnd.ActualizationShiftNextPeriod = false;
        vnd.ActualizationPlannedNoChanges = false;
        vnd.ActualizationPerformed = false;

        await _db.SaveChangesAsync();

        var developerHeadId = await _db.OrganizationUnits
            .Where(x => x.Id == vnd.DeveloperId)
            .Select(x => x.HeadUserId)
            .FirstOrDefaultAsync();

        await NotifyAsync(
            VndActualizationNotificationMessages.Published(vnd.TitleRu, request.HadChanges),
            vndId, currentUserId,
            developerHeadId.HasValue ? [developerHeadId.Value] : []);

        return await BuildStateResponseAsync(vnd);
    }

    /// <summary>История циклов актуализации документа, от самого нового к самому старому</summary>
    public async Task<List<VndActualizationRecordResponse>> GetHistoryAsync(int vndId)
    {
        var exists = await _db.VndDocuments.AnyAsync(x => x.Id == vndId);
        if (!exists) throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var records = await _db.Set<VndActualizationRecord>()
            .Include(x => x.ResponsibleUser)
            .Where(x => x.VndId == vndId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();

        return records.Select(x => new VndActualizationRecordResponse
        {
            Id = x.Id,
            ResponsibleUserId = x.ResponsibleUserId,
            ResponsibleUserName = x.ResponsibleUser?.FullName ?? "—",
            RequiresApproval = x.RequiresApproval,
            ShiftNextPeriod = x.ShiftNextPeriod,
            PlannedNoChanges = x.PlannedNoChanges,
            StartedAt = x.StartedAt,
            PerformedAt = x.PerformedAt,
            ConsolidationStartedAt = x.ConsolidationStartedAt,
            PublishedAt = x.PublishedAt,
            HadChanges = x.HadChanges,
            DueActualizationDateBefore = x.DueActualizationDateBefore,
            DueActualizationDateAfter = x.DueActualizationDateAfter,
            IsCompleted = x.PublishedAt.HasValue
        }).ToList();
    }

    /// <summary>Все заявки на доступ к актуализации этого документа (любого статуса), от новых
    /// к старым — для истории/аудита и для того, чтобы заявитель мог узнать статус своей заявки.</summary>
    public async Task<List<VndActualizationRequestResponse>> GetRequestHistoryAsync(int vndId)
    {
        var exists = await _db.VndDocuments.AnyAsync(x => x.Id == vndId);
        if (!exists) throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var requests = await _db.VndActualizationRequests
            .Include(x => x.Vnd)
            .Include(x => x.RequestedByUser)
            .Include(x => x.DecidedByUser)
            .Where(x => x.VndId == vndId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return requests.Select(ToRequestResponse).ToList();
    }

    private static DateOnly ResolveShiftedDueDate(ActualizationPeriod period, DateOnly today) => period switch
    {
        ActualizationPeriod.Quarterly => today.AddMonths(3),
        ActualizationPeriod.HalfYear => today.AddMonths(6),
        ActualizationPeriod.Annual => today.AddMonths(12),
        ActualizationPeriod.Biennial => today.AddMonths(24),
        ActualizationPeriod.Triennial => today.AddMonths(36),
        ActualizationPeriod.Custom => throw new InvalidOperationException(
            "Период Custom обрабатывается отдельно через NewDueActualizationDate"),
        _ => throw new InvalidOperationException("Неизвестный период актуализации")
    };

    private async Task<List<int>> GetChiefEditorIdsAsync() =>
        await _db.Users
            .Where(u => u.Roles.Any(r =>
                r.PermissionCodes.Contains((int)PermissionCode.ActualizeAnyVndWithApproval) ||
                r.PermissionCodes.Contains((int)PermissionCode.ActualizeAnyVndWithoutApproval)))
            .Select(u => u.Id)
            .ToListAsync();

    private async Task<VndActualizationStateResponse> BuildStateResponseAsync(VndDocument vnd)
    {
        string? responsibleName = null;
        if (vnd.ActualizationResponsibleUserId.HasValue)
        {
            responsibleName = await _db.Users
                .Where(u => u.Id == vnd.ActualizationResponsibleUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();
        }

        return new VndActualizationStateResponse
        {
            VndId = vnd.Id,
            Status = MapStatusBack(vnd.Status),
            ActualizationResponsibleUserId = vnd.ActualizationResponsibleUserId,
            ActualizationResponsibleUserName = responsibleName,
            ActualizationRequiresApproval = vnd.ActualizationRequiresApproval,
            ActualizationShiftNextPeriod = vnd.ActualizationShiftNextPeriod,
            ActualizationPlannedNoChanges = vnd.ActualizationPlannedNoChanges,
            ActualizationPerformed = vnd.ActualizationPerformed,
            DueActualizationDate = vnd.DueActualizationDate,
            LastActualizationDate = vnd.LastActualizationDate
        };
    }

    private async Task<VndActualizationRequestResponse> LoadRequestResponseAsync(int requestId)
    {
        var request = await _db.VndActualizationRequests
            .Include(x => x.Vnd)
            .Include(x => x.RequestedByUser)
            .Include(x => x.DecidedByUser)
            .FirstAsync(x => x.Id == requestId);

        return ToRequestResponse(request);
    }

    private static VndActualizationRequestResponse ToRequestResponse(VndActualizationRequest x) => new()
    {
        Id = x.Id,
        VndId = x.VndId,
        VndCode = x.Vnd?.Code ?? "",
        VndTitle = x.Vnd?.TitleRu ?? "",
        RequestedByUserId = x.RequestedByUserId,
        RequestedByName = x.RequestedByUser?.FullName ?? "",
        RequiresApproval = x.RequiresApproval,
        ShiftNextPeriod = x.ShiftNextPeriod,
        Status = x.Status switch
        {
            ActualizationAccessStatus.Pending => "pending",
            ActualizationAccessStatus.Approved => "approved",
            ActualizationAccessStatus.Rejected => "rejected",
            _ => "pending"
        },
        DecidedByUserId = x.DecidedByUserId,
        DecidedByName = x.DecidedByUser?.FullName,
        DecidedAt = x.DecidedAt,
        ConsumedAt = x.ConsumedAt,
        CreatedAt = x.CreatedAt
    };

    private static string MapStatusBack(VndStatus status) => status switch
    {
        VndStatus.Active => "active",
        VndStatus.OnActualization => "onact",
        VndStatus.Review => "review",
        VndStatus.Consolidation => "consol",
        VndStatus.Archived => "arch",
        VndStatus.Draft => "draft",
        _ => "onact"
    };

    private async Task NotifyAsync(
        NotificationText text, int vndId, int? triggeredByUserId, int[] recipientUserIds,
        string? urlOverride = null)
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
                Category = NotificationCategory.Approval,
                Severity = text.Severity,
                EntityType = "Vnd",
                EntityId = vndId,
                Url = urlOverride ?? $"/base-vnd/{vndId}",
                UserIds = recipientUserIds.Distinct().ToList()
            }, triggeredByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Не удалось отправить уведомление по актуализации ВНД (vndId={VndId})", vndId);
        }
    }
}
