using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Models;
using delosfera_server.Modules.Vnd.Notifications;

namespace delosfera_server.Modules.Vnd.Services;

public class VndActualizationService : IVndActualizationService
{
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILogger<VndActualizationService> _logger;

    public VndActualizationService(
        DelosferaDbContext db,
        ICurrentUserService currentUser,
        INotificationService notifications,
        ILogger<VndActualizationService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _logger = logger;
    }

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

        var responsibleUserId = request.ResponsibleUserId ?? currentUserId;
        var responsibleExists = await _db.Users.AnyAsync(x => x.Id == responsibleUserId);
        if (!responsibleExists)
            throw new KeyNotFoundException($"Пользователь с id={responsibleUserId} не найден");

        vnd.Status = VndStatus.OnActualization;
        vnd.ActualizationResponsibleUserId = responsibleUserId;
        vnd.ActualizationRequiresApproval = request.RequiresApproval;
        vnd.ActualizationShiftNextPeriod = request.ShiftNextPeriod;

        // --- Открываем запись в истории циклов актуализации
        _db.Set<VndActualizationRecord>().Add(new VndActualizationRecord
        {
            VndId = vndId,
            ResponsibleUserId = responsibleUserId,
            RequiresApproval = request.RequiresApproval,
            ShiftNextPeriod = request.ShiftNextPeriod,
            StartedAt = DateTime.UtcNow,
            DueActualizationDateBefore = vnd.DueActualizationDate
        });

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
            Status = ActualizationAccessStatus.Pending
        };

        _db.VndActualizationRequests.Add(entity);
        await _db.SaveChangesAsync();

        var requester = await _db.Users.FindAsync(currentUserId);
        var chiefEditorIds = await GetChiefEditorIdsAsync();

        await NotifyAsync(
            VndActualizationNotificationMessages.AccessRequested(vnd.TitleRu, requester?.FullName ?? "—"),
            vndId, currentUserId, chiefEditorIds.ToArray());

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

    public async Task<VndActualizationRequestResponse> DecideRequestAsync(
        int requestId, bool approve, int currentUserId)
    {
        var canWithoutApproval = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);
        var canWithApproval = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval);
        if (!canWithoutApproval && !canWithApproval)
            throw new UnauthorizedAccessException("Решения по заявкам принимает только главный редактор ВНД");

        var request = await _db.VndActualizationRequests
            .Include(x => x.Vnd)
            .FirstOrDefaultAsync(x => x.Id == requestId)
            ?? throw new KeyNotFoundException($"Заявка с id={requestId} не найдена");

        if (request.Status != ActualizationAccessStatus.Pending)
            throw new InvalidOperationException("Решение по этой заявке уже принято");

        request.Status = approve ? ActualizationAccessStatus.Approved : ActualizationAccessStatus.Rejected;
        request.DecidedByUserId = currentUserId;
        request.DecidedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var notice = approve
            ? VndActualizationNotificationMessages.AccessApproved(request.Vnd!.TitleRu)
            : VndActualizationNotificationMessages.AccessRejected(request.Vnd!.TitleRu);

        await NotifyAsync(notice, request.VndId, currentUserId, request.RequestedByUserId);

        return await LoadRequestResponseAsync(request.Id);
    }

    public async Task<VndActualizationStateResponse> ConfirmStartAfterRequestAsync(
        int vndId, ConfirmActualizationStartRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
            ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.Active)
            throw new InvalidOperationException("Начать актуализацию можно только для действующего ВНД");

        // Берём самую свежую одобренную заявку текущего пользователя по этому ВНД.
        // NB: заявка не "гасится" после использования — если нужно ограничить
        // повторный запуск без новой заявки на следующий цикл, добавь сюда доп. флаг.
        var approvedRequest = await _db.VndActualizationRequests
            .Where(x => x.VndId == vndId && x.RequestedByUserId == currentUserId
                                          && x.Status == ActualizationAccessStatus.Approved)
            .OrderByDescending(x => x.DecidedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "Нет одобренной заявки на актуализацию этого ВНД для текущего пользователя");

        vnd.Status = VndStatus.OnActualization;
        vnd.ActualizationResponsibleUserId = currentUserId;
        vnd.ActualizationRequiresApproval = approvedRequest.RequiresApproval;
        vnd.ActualizationShiftNextPeriod = request.ShiftNextPeriod;

        // --- Открываем запись в истории циклов актуализации
        _db.Set<VndActualizationRecord>().Add(new VndActualizationRecord
        {
            VndId = vndId,
            ResponsibleUserId = currentUserId,
            RequiresApproval = approvedRequest.RequiresApproval,
            ShiftNextPeriod = request.ShiftNextPeriod,
            StartedAt = DateTime.UtcNow,
            DueActualizationDateBefore = vnd.DueActualizationDate
        });

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

        var isChiefEditor = _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval)
                            || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);

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
            StartedAt = x.StartedAt,
            PublishedAt = x.PublishedAt,
            HadChanges = x.HadChanges,
            DueActualizationDateBefore = x.DueActualizationDateBefore,
            DueActualizationDateAfter = x.DueActualizationDateAfter,
            IsCompleted = x.PublishedAt.HasValue
        }).ToList();
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
        Notifications.NotificationText text, int vndId, int? triggeredByUserId, params int[] recipientUserIds)
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
                Category = NotificationCategory.Approval, // TODO: завести отдельную категорию Actualization
                Severity = text.Severity,
                EntityType = "Vnd",
                EntityId = vndId,
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