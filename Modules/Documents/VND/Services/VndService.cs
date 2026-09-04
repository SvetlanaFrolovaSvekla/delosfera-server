using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.ActivityLog.Models;
using delosfera_server.Modules.ActivityLog.Services;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public class VndService : IVndService
{
    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _fileService;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogService _activityLog;
    private readonly IVndApprovalService _approvalService;

    public VndService(
        DelosferaDbContext db, IFileStorageService fileService,
        ICurrentUserService currentUser, IActivityLogService activityLog,
        IVndApprovalService approvalService)
    {
        _db = db;
        _fileService = fileService;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _approvalService = approvalService;
    }

    public async Task<List<VndResponse>> SearchAsync(VndSearchRequest request, string languageCode)
    {
        // Вычисляется здесь (а не только перед ToListAsync, как раньше), т.к. теперь используется
        // и для ограничения видимости "Статуса ВНД" (документ-уровня) ниже.
        var canViewExtended = _currentUser.HasPermission(PermissionCode.ViewVndRegistryExtended);

        IQueryable<VndDocument> query = _db.VndDocuments
            .Include(x => x.Type)
            .Include(x => x.Developer)
            .Include(x => x.CuratorDeveloper)
            .Include(x => x.Organ)
            .Include(x => x.ResponsibleExecutors)
            .Include(x => x.Rubrics)
            .Include(x => x.Keywords)
            .Include(x => x.UserGroups)
            .Include(x => x.Redactions)
            .Include(x => x.CreatedByUser)
            .Include(x => x.ActualizationResponsibleUser);

        if (!string.IsNullOrWhiteSpace(request.Code))
            query = query.Where(x => EF.Functions.ILike(x.Code, $"%{request.Code}%"));

        if (!string.IsNullOrWhiteSpace(request.Name))
            query = query.Where(x =>
                EF.Functions.ILike(x.TitleRu, $"%{request.Name}%") ||
                (x.TitleEn != null && EF.Functions.ILike(x.TitleEn, $"%{request.Name}%")) ||
                (x.TitleKg != null && EF.Functions.ILike(x.TitleKg, $"%{request.Name}%")));

        if (request.Statuses.Count > 0)
        {
            var statuses = request.Statuses.Select(MapStatus).ToList();
            query = query.Where(x => statuses.Contains(x.Status));
        }

        // "Статус ВНД" (документ-уровня): пользователям без ViewVndRegistryExtended документы
        // "ещё не действующие" (notYetActive) не показываются вообще — ни в одном табе/scope, а
        // не сворачиваются в "действующий", как было раньше. Это ограничение видимости
        // применяется безусловно (не зависит от того, задан ли request.DocumentStatuses), потому
        // что сервер для таких пользователей в принципе не должен отдавать эти документы —
        // см. также CollapseDocumentStatus (используется только для GetById по прямой ссылке,
        // не для реестра). Предикат — отрицание "notYetActive" из ComputeDocumentStatus ниже,
        // продублированное в EF-транслируемом виде (см. тот же приём в фильтре
        // DocumentStatuses ниже).
        if (!canViewExtended)
        {
            query = query.Where(x =>
                x.Status == VndStatus.Archived ||
                x.Status == VndStatus.Active ||
                x.ActualizationResponsibleUserId != null ||
                x.Redactions.Count > 1);
        }

        if (request.DocumentStatuses.Count > 0)
        {
            // Фильтр по "Статусу ВНД" (документ-уровня) — независимая от Statuses выше ось.
            // Доступен фактически только пользователям с ViewVndRegistryExtended (см. фильтр
            // видимости выше — для остальных notYetActive уже вырезан безусловно, а "active"/
            // "arch" эквивалентны обычной фильтрации). Используется как фильтром в VndFilters,
            // так и новым табом "Ещё не действующие" (scope="notYetActive", см. useVndFilters).
            // Логика продублирована в виде EF-транслируемого предиката (а не переиспользует
            // ComputeDocumentStatus напрямую — тот рассчитан на уже загруженную в память
            // сущность и не транслируется в SQL). См. ComputeDocumentStatus ниже — то же самое
            // правило, применённое на чтении к уже загруженным документам.
            var wantsActive = request.DocumentStatuses.Contains("active");
            var wantsNotYetActive = request.DocumentStatuses.Contains("notYetActive");
            var wantsArch = request.DocumentStatuses.Contains("arch");

            query = query.Where(x =>
                (wantsArch && x.Status == VndStatus.Archived) ||
                (wantsNotYetActive && x.Status != VndStatus.Archived && x.Redactions.Count <= 1 &&
                 x.Status != VndStatus.Active && x.ActualizationResponsibleUserId == null) ||
                (wantsActive && x.Status != VndStatus.Archived &&
                 !(x.Redactions.Count <= 1 && x.Status != VndStatus.Active &&
                   x.ActualizationResponsibleUserId == null)));
        }

        if (request.TypeIds.Count > 0)
            query = query.Where(x => request.TypeIds.Contains(x.TypeId));

        if (request.OrganIds.Count > 0)
            query = query.Where(x => request.OrganIds.Contains(x.OrganId));

        if (request.DeveloperIds.Count > 0)
            query = query.Where(x => request.DeveloperIds.Contains(x.DeveloperId));

        if (request.ResponsibleExecutorIds.Count > 0)
            query = query.Where(x => x.ResponsibleExecutors.Any(e => request.ResponsibleExecutorIds.Contains(e.Id)));

        if (request.CreatedByUserIds.Count > 0)
            query = query.Where(x =>
                x.CreatedByUserId != null && request.CreatedByUserIds.Contains(x.CreatedByUserId.Value));

        if (request.KeywordIds.Count > 0)
            query = query.Where(x => x.Keywords.Any(k => request.KeywordIds.Contains(k.Id)));

        if (request.RubricIds.Count > 0)
            query = query.Where(x => x.Rubrics.Any(r => request.RubricIds.Contains(r.Id)));

        if (request.SecrecyLevelIds.Count > 0)
            query = query.Where(x => request.SecrecyLevelIds.Contains(x.SecrecyLevelId));

        if (request.UserGroupIds.Count > 0)
            query = query.Where(x => x.UserGroups.Any(g => request.UserGroupIds.Contains(g.Id)));

        if (!string.IsNullOrWhiteSpace(request.AdoptionCode))
            query = query.Where(x =>
                x.AdoptionCode != null && EF.Functions.ILike(x.AdoptionCode, $"%{request.AdoptionCode}%"));

        if (!string.IsNullOrWhiteSpace(request.CancelCode))
            query =
                query.Where(x => x.CancelCode != null && EF.Functions.ILike(x.CancelCode, $"%{request.CancelCode}%"));

        query = ApplyDateFilter(query, request.AdoptionDate, x => x.AdoptionDate);
        query = ApplyDateFilter(query, request.EffectiveDate, x => x.EffectiveDate);
        query = ApplyDateFilter(query, request.RequisitesChangedDate, x => x.RequisitesChangedDate);
        query = ApplyDateFilter(query, request.RevisionChangedDate, x => x.RevisionChangedDate);
        query = ApplyDateFilter(query, request.CancelDate, x => x.CancelDate);
        query = ApplyDateFilter(query, request.DueActualizationDate, x => x.DueActualizationDate);
        query = ApplyDateFilter(query, request.LastActualizationDate, x => x.LastActualizationDate);
        query = ApplyDateFilter(query, request.ArchivedDate, x => x.ArchivedDate);

        query = ApplyActualizationBucketFilter(query, request.ActualizationBuckets);

        query = ApplyLinkedToMeFilter(query, request.LinkedToMeOnly, request.LinkedToMeRelations);
        query = ApplyDraftVisibilityFilter(query, request.DraftOwnerScope);

        var entities = await query.ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Виды связи с текущим пользователем считаем только когда запрошен LinkedToMeOnly —
        // это отдельные запросы к БД, незачем тратить их, когда колонка "Связь со мной" всё
        // равно скрыта на фронте
        var relationsByVndId = request.LinkedToMeOnly
            ? await BuildLinkedToMeRelationsAsync(entities)
            : null;

        return entities
            .Select(x => ToResponse(x, languageCode, today, canViewExtended, relationsByVndId?.GetValueOrDefault(x.Id)))
            .ToList();
    }

    /// <summary>Для каждого документа из <paramref name="entities"/> — список видов связи
    /// текущего пользователя с ним (см. VndResponse.LinkedToMeRelations и ApplyLinkedToMeFilter
    /// для семантики каждого ключа). В отличие от ApplyLinkedToMeFilter, здесь не важно, что
    /// выбрано в фильтре — показываем ВСЕ фактические связи, а не только отмеченные галочками.</summary>
    private async Task<Dictionary<int, List<string>>> BuildLinkedToMeRelationsAsync(List<VndDocument> entities)
    {
        var result = entities.ToDictionary(e => e.Id, _ => new List<string>());
        if (entities.Count == 0) return result;

        var userId = _currentUser.UserId;
        var ids = result.Keys.ToList();

        foreach (var d in entities)
        {
            if (d.CreatedByUserId == userId) result[d.Id].Add("initiator");
            if (d.ActualizationResponsibleUserId == userId && d.Status == VndStatus.OnActualization)
                result[d.Id].Add("currentActualizer");
            if (d.ActualizationResponsibleUserId == userId && d.Status == VndStatus.Consolidation)
                result[d.Id].Add("currentConsolidator");
        }

        var approverProcesses = await _db.VndApprovalProcesses
            .Where(p => ids.Contains(p.VndId) && p.Stages.Any(s => s.ApproverUserId == userId))
            .Select(p => new {p.VndId, p.CompletedAt})
            .ToListAsync();

        foreach (var p in approverProcesses)
            result[p.VndId].Add(p.CompletedAt == null ? "currentApprover" : "pastApprover");

        var records = await _db.Set<VndActualizationRecord>()
            .Where(r => ids.Contains(r.VndId) && r.ResponsibleUserId == userId && r.PublishedAt != null)
            .Select(r => new {r.VndId, r.ConsolidationStartedAt})
            .ToListAsync();

        foreach (var r in records)
        {
            result[r.VndId].Add("pastActualizer");
            if (r.ConsolidationStartedAt != null) result[r.VndId].Add("pastConsolidator");
        }

        return result;
    }

    public async Task<VndResponse> GetByIdAsync(int id, string languageCode)
    {
        var entity = await _db.VndDocuments
                         .Include(x => x.Type)
                         .Include(x => x.Developer)
                         .Include(x => x.CuratorDeveloper)
                         .Include(x => x.Organ)
                         .Include(x => x.ResponsibleExecutors)
                         .Include(x => x.Rubrics)
                         .Include(x => x.Keywords)
                         .Include(x => x.UserGroups)
                         .Include(x => x.Redactions)
                         .Include(x => x.CreatedByUser)
                         .Include(x => x.ActualizationResponsibleUser)
                         .FirstOrDefaultAsync(x => x.Id == id)
                     ?? throw new KeyNotFoundException($"ВНД с id={id} не найден");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var canViewExtended = _currentUser.HasPermission(PermissionCode.ViewVndRegistryExtended);
        return ToResponse(entity, languageCode, today, canViewExtended);
    }

    /// <summary>Сводка по срокам актуализации для дашборда планирования.
    /// Документы без DueActualizationDate (архив/черновики) не учитываются.
    /// Считается одним SQL-запросом через условные COUNT.</summary>
    public async Task<VndActualizationSummaryResponse> GetActualizationSummaryAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var criticalEnd = today.AddDays(ActualizationThresholds.CriticalDays);
        var approachingEnd = today.AddDays(ActualizationThresholds.ApproachingDays);

        var counts = await _db.VndDocuments
            .Where(x => x.DueActualizationDate != null)
            .GroupBy(x => 1)
            .Select(g => new VndActualizationSummaryResponse
            {
                Overdue = g.Count(x => x.DueActualizationDate!.Value < today),
                Critical = g.Count(x =>
                    x.DueActualizationDate!.Value >= today && x.DueActualizationDate!.Value <= criticalEnd),
                Approaching = g.Count(x =>
                    x.DueActualizationDate!.Value > criticalEnd && x.DueActualizationDate!.Value <= approachingEnd),
                Normal = g.Count(x => x.DueActualizationDate!.Value > approachingEnd)
            })
            .FirstOrDefaultAsync() ?? new VndActualizationSummaryResponse();

        counts.Total = counts.Normal + counts.Approaching + counts.Critical + counts.Overdue;
        return counts;
    }

    private static IQueryable<VndDocument> ApplyActualizationBucketFilter(
        IQueryable<VndDocument> query, List<string> bucketKeys)
    {
        if (bucketKeys.Count == 0) return query;

        var buckets = bucketKeys.Select(MapActualizationBucketKey).ToHashSet();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var criticalEnd = today.AddDays(ActualizationThresholds.CriticalDays);
        var approachingEnd = today.AddDays(ActualizationThresholds.ApproachingDays);

        var includeNormal = buckets.Contains(ActualizationBucket.Normal);
        var includeApproaching = buckets.Contains(ActualizationBucket.Approaching);
        var includeCritical = buckets.Contains(ActualizationBucket.Critical);
        var includeOverdue = buckets.Contains(ActualizationBucket.Overdue);

        return query.Where(x =>
            x.DueActualizationDate != null && (
                (includeOverdue && x.DueActualizationDate.Value < today) ||
                (includeCritical && x.DueActualizationDate.Value >= today &&
                 x.DueActualizationDate.Value <= criticalEnd) ||
                (includeApproaching && x.DueActualizationDate.Value > criticalEnd &&
                 x.DueActualizationDate.Value <= approachingEnd) ||
                (includeNormal && x.DueActualizationDate.Value > approachingEnd)
            ));
    }

    /// <summary>"Только связанные со мной" — фильтрует по конкретным видам связи из
    /// <paramref name="relations"/> (пусто = не совпадёт ни с чем; фронт всегда передаёт явный
    /// список ключей, по умолчанию — все):
    /// - initiator — пользователь создал документ (VndDocument.CreatedByUserId);
    /// - currentApprover / pastApprover — согласующий на одном из этапов активного /
    ///   уже завершённого процесса согласования;
    /// - currentActualizer / currentConsolidator — ответственный за текущий цикл
    ///   актуализации (VndDocument.ActualizationResponsibleUserId), различаются по
    ///   текущему статусу документа (OnActualization / Consolidation);
    /// - pastActualizer — был ответственным в завершённом цикле актуализации
    ///   (VndActualizationRecord.PublishedAt != null);
    /// - pastConsolidator — тот же завершённый цикл, но только если в нём реально была
    ///   стадия консолидации (VndActualizationRecord.ConsolidationStartedAt != null) — для
    ///   циклов без согласования, опубликованных напрямую из OnActualization минуя
    ///   Consolidation, pastConsolidator не сработает, даже если pastActualizer сработает.</summary>
    private IQueryable<VndDocument> ApplyLinkedToMeFilter(
        IQueryable<VndDocument> query, bool linkedToMeOnly, List<string> relations)
    {
        if (!linkedToMeOnly) return query;

        var userId = _currentUser.UserId;
        var rel = relations.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var wantInitiator = rel.Contains("initiator");
        var wantCurrentApprover = rel.Contains("currentApprover");
        var wantPastApprover = rel.Contains("pastApprover");
        var wantCurrentActualizer = rel.Contains("currentActualizer");
        var wantPastActualizer = rel.Contains("pastActualizer");
        var wantCurrentConsolidator = rel.Contains("currentConsolidator");
        var wantPastConsolidator = rel.Contains("pastConsolidator");

        return query.Where(x =>
            (wantInitiator && x.CreatedByUserId == userId) ||
            (wantCurrentApprover && _db.VndApprovalProcesses.Any(p =>
                p.VndId == x.Id && p.CompletedAt == null && p.Stages.Any(s => s.ApproverUserId == userId))) ||
            (wantPastApprover && _db.VndApprovalProcesses.Any(p =>
                p.VndId == x.Id && p.CompletedAt != null && p.Stages.Any(s => s.ApproverUserId == userId))) ||
            (wantCurrentActualizer &&
             x.ActualizationResponsibleUserId == userId && x.Status == VndStatus.OnActualization) ||
            (wantCurrentConsolidator &&
             x.ActualizationResponsibleUserId == userId && x.Status == VndStatus.Consolidation) ||
            (wantPastActualizer && _db.Set<VndActualizationRecord>().Any(r =>
                r.VndId == x.Id && r.ResponsibleUserId == userId && r.PublishedAt != null)) ||
            (wantPastConsolidator && _db.Set<VndActualizationRecord>().Any(r =>
                r.VndId == x.Id && r.ResponsibleUserId == userId &&
                r.PublishedAt != null && r.ConsolidationStartedAt != null)));
    }

    /// <summary>Фиксирует момент входа документа в статус "Консолидация" в открытой (ещё не
    /// опубликованной) записи истории актуализации — используется фильтром "Только связанные
    /// со мной" (виды связи "я консолидирую" / "я когда-то консолидировал"). Не пишет в БД сама
    /// (SaveChangesAsync вызывает вызывающий код) и ничего не делает, если открытой записи нет
    /// (например, редакция без согласования вне цикла актуализации) или отметка уже стоит.</summary>
    private async Task StampConsolidationStartedAsync(int vndId)
    {
        var openRecord = await _db.Set<VndActualizationRecord>()
            .Where(r => r.VndId == vndId && r.PublishedAt == null)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (openRecord is not null && openRecord.ConsolidationStartedAt is null)
            openRecord.ConsolidationStartedAt = DateTime.UtcNow;
    }

    /// <summary>Видимость черновиков: пользователь без права ViewOtherUsersDrafts никогда не
    /// видит чужие черновики (проверка применяется всегда, а не только на вкладке "Черновики",
    /// чтобы черновики других не просачивались, например, через вкладку "Все"). DraftOwnerScope
    /// дополнительно сужает список ("mine"/"others") — "others" учитывается, только если право есть.</summary>
    private IQueryable<VndDocument> ApplyDraftVisibilityFilter(IQueryable<VndDocument> query, string? draftOwnerScope)
    {
        var userId = _currentUser.UserId;
        var canViewOtherDrafts = _currentUser.HasPermission(PermissionCode.ViewOtherUsersDrafts);

        query = query.Where(x =>
            x.Status != VndStatus.Draft || canViewOtherDrafts || x.CreatedByUserId == userId);

        if (string.IsNullOrWhiteSpace(draftOwnerScope)) return query;

        var wantsOthers = draftOwnerScope.Equals("others", StringComparison.OrdinalIgnoreCase) && canViewOtherDrafts;

        return wantsOthers
            ? query.Where(x => x.Status != VndStatus.Draft || x.CreatedByUserId != userId)
            : query.Where(x => x.Status != VndStatus.Draft || x.CreatedByUserId == userId);
    }

    /// <summary>Главный редактор — пользователь с любым из «сквозных» прав на создание/актуализацию
    /// ВНД; такой пользователь причастен к любому документу без явной привязки.</summary>
    private bool IsChiefEditor() =>
        _currentUser.HasPermission(PermissionCode.CreateVndWithApproval)
        || _currentUser.HasPermission(PermissionCode.CreateVndWithoutApproval)
        || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithApproval)
        || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);

    /// <summary>Право реально обойти согласование - строже, чем IsChiefEditor(). Права
    /// CreateVndWithApproval/ActualizeAnyVndWithApproval дают возможность создавать/актуализировать
    /// ВНД, но результат всё равно уходит на согласование - наличие только одного из них (например,
    /// у роли "Редактор ВНД") не должно позволять пропустить согласование целиком (см.
    /// PublishRedactionWithoutApprovalAsync ниже). IsChiefEditor() шире и используется отдельно -
    /// для доступа к документам, к которым пользователь явно не привязан.</summary>
    private bool CanPublishWithoutApproval() =>
        _currentUser.HasPermission(PermissionCode.CreateVndWithoutApproval)
        || _currentUser.HasPermission(PermissionCode.ActualizeAnyVndWithoutApproval);

    private static ActualizationBucket MapActualizationBucketKey(string key) => key.ToLowerInvariant() switch
    {
        "normal" => ActualizationBucket.Normal,
        "approaching" => ActualizationBucket.Approaching,
        "critical" => ActualizationBucket.Critical,
        "overdue" => ActualizationBucket.Overdue,
        _ => throw new InvalidOperationException($"Неизвестный статус срока актуализации: {key}")
    };

    private static string? MapActualizationBucketBack(ActualizationBucket? bucket) => bucket switch
    {
        ActualizationBucket.Normal => "normal",
        ActualizationBucket.Approaching => "approaching",
        ActualizationBucket.Critical => "critical",
        ActualizationBucket.Overdue => "overdue",
        _ => null
    };

    // Строим предикат из дерева выражений selector, а НЕ из скомпилированного делегата:
    // вызов Compile()+делегата внутри Where EF Core не может транслировать в SQL и падает.
    private static IQueryable<VndDocument> ApplyDateFilter(
        IQueryable<VndDocument> query, DateRangeFilter? filter,
        Expression<Func<VndDocument, DateOnly?>> selector)
    {
        if (filter is null) return query;

        var param = selector.Parameters[0];
        var value = selector.Body;
        var nonNull = Expression.Property(value, nameof(Nullable<DateOnly>.Value));
        var isNotNull = Expression.NotEqual(value, Expression.Constant(null, typeof(DateOnly?)));

        Expression<Func<VndDocument, bool>> Lambda(Expression body) =>
            Expression.Lambda<Func<VndDocument, bool>>(body, param);

        if (filter.Exact.HasValue)
        {
            var eq = Expression.Equal(nonNull, Expression.Constant(filter.Exact.Value, typeof(DateOnly)));
            return query.Where(Lambda(Expression.AndAlso(isNotNull, eq)));
        }

        if (filter.From.HasValue)
        {
            var ge = Expression.GreaterThanOrEqual(nonNull, Expression.Constant(filter.From.Value, typeof(DateOnly)));
            query = query.Where(Lambda(Expression.AndAlso(isNotNull, ge)));
        }

        if (filter.To.HasValue)
        {
            var le = Expression.LessThanOrEqual(nonNull, Expression.Constant(filter.To.Value, typeof(DateOnly)));
            query = query.Where(Lambda(Expression.AndAlso(isNotNull, le)));
        }

        return query;
    }

    private static VndStatus MapStatus(string key) => key switch
    {
        "active" => VndStatus.Active,
        "onact" => VndStatus.OnActualization,
        "review" => VndStatus.Review,
        "consol" => VndStatus.Consolidation,
        "arch" => VndStatus.Archived,
        "draft" => VndStatus.Draft,
        _ => throw new InvalidOperationException($"Неизвестный статус: {key}")
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

    /// <summary>"Статус ВНД" (документ-уровня) — см. подробное описание в VndResponse.DocumentStatus.
    /// Вычисляется на чтении, здесь работает с уже загруженной в память сущностью (x.Redactions
    /// должна быть загружена — Include(x => x.Redactions) есть во всех местах, откуда вызывается
    /// ToResponse). Та же логика в EF-транслируемом виде — см. фильтр по DocumentStatuses
    /// в SearchAsync.</summary>
    private static string ComputeDocumentStatus(VndDocument x)
    {
        if (x.Status == VndStatus.Archived) return "arch";

        // "Был ли документ хоть раз действующим": сам статус Active, либо документ сейчас
        // находится в цикле актуализации (ActualizationResponsibleUserId != null) — а цикл
        // актуализации можно начать только для уже действующего документа (см.
        // VndActualizationService.StartAsync: "Начать актуализацию можно только для
        // действующего ВНД"), и это поле не сбрасывается в процессе цикла (сбрасывается
        // только по завершении публикации, VndActualizationService.PublishAsync) — поэтому его
        // непустое значение надёжно говорит "документ уже был Active", даже если сейчас
        // документ, например, снова на согласовании (Review) в рамках того же цикла.
        var everWasActive = x.Status == VndStatus.Active || x.ActualizationResponsibleUserId != null;

        // "Есть максимум одна редакция за всю историю" (0 — только что создан ВНД, редакция ещё
        // не загружена; 1 — загружена первая и единственная) — редакции никогда не удаляются,
        // поэтому Count <= 1 эквивалентно "текущая/единственная редакция, если есть, имеет
        // Number == 1".
        if (x.Redactions.Count <= 1 && !everWasActive) return "notYetActive";

        return "active";
    }

    /// <summary>Сворачивает "notYetActive" в "active" для пользователей без права
    /// ViewVndRegistryExtended — такие пользователи всегда видели подобные документы как
    /// "действующие" (детали жизненного цикла им и так недоступны) и не должны получать
    /// 3-е значение статуса ВНД. Делается на сервере (не только на фронте), т.к. это дёшево —
    /// у VndService уже есть ICurrentUserService.HasPermission под рукой.</summary>
    private static string CollapseDocumentStatus(string raw, bool canViewExtended) =>
        !canViewExtended && raw == "notYetActive" ? "active" : raw;

    private static VndResponse ToResponse(
        VndDocument x, string languageCode, DateOnly today, bool canViewExtended,
        List<string>? linkedToMeRelations = null) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.ResolveTitle(languageCode),
        TitleRu = x.TitleRu,
        TitleEn = x.TitleEn,
        TitleKg = x.TitleKg,
        Status = MapStatusBack(x.Status),
        DocumentStatus = CollapseDocumentStatus(ComputeDocumentStatus(x), canViewExtended),
        TypeId = x.TypeId,
        TypeName = x.Type?.TitleRu ?? "",
        DeveloperId = x.DeveloperId,
        DeveloperName = x.Developer?.TitleRu ?? "",
        CuratorDeveloperId = x.CuratorDeveloperId,
        CuratorDeveloperName = x.CuratorDeveloper?.FullName,
        OrganId = x.OrganId,
        OrganName = x.Organ?.TitleRu ?? "",
        ResponsibleExecutorIds = x.ResponsibleExecutors.Select(e => e.Id).ToList(),
        CreatedByUserId = x.CreatedByUserId,
        CreatedByUserName = x.CreatedByUser?.FullName,
        ActualizationResponsibleUserId = x.ActualizationResponsibleUserId,
        ActualizationResponsibleUserName = x.ActualizationResponsibleUser?.FullName,
        ActualizationRequiresApproval = x.ActualizationRequiresApproval,
        ActualizationPlannedNoChanges = x.ActualizationPlannedNoChanges,
        ActualizationShiftNextPeriod = x.ActualizationShiftNextPeriod,
        ActualizationPerformed = x.ActualizationPerformed,
        AdoptionDate = x.AdoptionDate,
        AdoptionCode = x.AdoptionCode,
        EffectiveDate = x.EffectiveDate,
        RequisitesChangedDate = x.RequisitesChangedDate,
        RevisionChangedDate = x.RevisionChangedDate,
        CancelDate = x.CancelDate,
        CancelCode = x.CancelCode,
        CancelReason = x.CancelReason,
        ArchivedDate = x.ArchivedDate,
        DueActualizationDate = x.DueActualizationDate,
        LastActualizationDate = x.LastActualizationDate,
        LastActualizationHadChanges = x.LastActualizationHadChanges,
        DaysInArchive = x.DaysInArchive,
        ActualizationBucket =
            MapActualizationBucketBack(ActualizationThresholds.Resolve(x.DueActualizationDate, today)),
        KeywordIds = x.Keywords.Select(k => k.Id).ToList(),
        RubricIds = x.Rubrics.Select(r => r.Id).ToList(),
        SecrecyLevelId = x.SecrecyLevelId,
        UserGroupIds = x.UserGroups.Select(g => g.Id).ToList(),
        RedactionIds = x.Redactions.Select(r => r.Id).ToList(),
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
        LinkedToMeRelations = linkedToMeRelations ?? []
    };

    /// <summary>Довешивает Include-ы, необходимые для реквизитов редакции (см. ToRedactionResponse
    /// ниже) — используется во всех местах, где грузится VndRedaction перед превращением в
    /// VndRedactionResponse, чтобы не забыть какой-нибудь Include и не получить пустые имена.</summary>
    private static IQueryable<VndRedaction> IncludeRequisites(IQueryable<VndRedaction> query) =>
        query.Include(x => x.Type)
            .Include(x => x.Developer)
            .Include(x => x.CuratorDeveloper)
            .Include(x => x.Organ)
            .Include(x => x.ResponsibleExecutors)
            .Include(x => x.Rubrics)
            .Include(x => x.Keywords);

    /// <summary>Копирует реквизиты в НОВУЮ редакцию (заголовок/вид, орган/разработчик/куратор,
    /// гриф секретности, период, ответственные исполнители/рубрики/ключевые слова) — либо с
    /// предыдущей редакции этого же ВНД (source), либо, если это первая редакция, с самого
    /// документа (vnd). Используется в AddRedactionAsync. Дату/номер утверждения и дату
    /// вступления в силу намеренно НЕ копирует — они всегда стартуют пустыми у новой редакции.</summary>
    private static void CopyRequisitesFrom(VndRedaction target, VndRedaction? source, VndDocument vnd)
    {
        if (source is not null)
        {
            target.TitleRu = source.TitleRu;
            target.TitleEn = source.TitleEn;
            target.TitleKg = source.TitleKg;
            target.TypeId = source.TypeId;
            target.Type = source.Type;
            target.DeveloperId = source.DeveloperId;
            target.Developer = source.Developer;
            target.CuratorDeveloperId = source.CuratorDeveloperId;
            target.CuratorDeveloper = source.CuratorDeveloper;
            target.OrganId = source.OrganId;
            target.Organ = source.Organ;
            target.SecrecyLevelId = source.SecrecyLevelId;
            target.SecrecyLevel = source.SecrecyLevel;
            target.Period = source.Period;
            target.ResponsibleExecutors = source.ResponsibleExecutors.ToList();
            target.Rubrics = source.Rubrics.ToList();
            target.Keywords = source.Keywords.ToList();
        }
        else
        {
            target.TitleRu = vnd.TitleRu;
            target.TitleEn = vnd.TitleEn;
            target.TitleKg = vnd.TitleKg;
            target.TypeId = vnd.TypeId;
            target.Type = vnd.Type;
            target.DeveloperId = vnd.DeveloperId;
            target.Developer = vnd.Developer;
            target.CuratorDeveloperId = vnd.CuratorDeveloperId;
            target.CuratorDeveloper = vnd.CuratorDeveloper;
            target.OrganId = vnd.OrganId;
            target.Organ = vnd.Organ;
            target.SecrecyLevelId = vnd.SecrecyLevelId;
            target.SecrecyLevel = vnd.SecrecyLevel;
            target.Period = vnd.Period;
            target.ResponsibleExecutors = vnd.ResponsibleExecutors.ToList();
            target.Rubrics = vnd.Rubrics.ToList();
            target.Keywords = vnd.Keywords.ToList();
        }
    }

    // vnd (а не просто currentRedactionId) — чтобы IsCurrent мог учитывать и Status: у
    // архивированного ВНД ни одна редакция больше не "текущая/действующая", даже та, что была
    // ею перед архивацией (CurrentRedactionId при этом не трогаем — он остаётся историческим
    // указателем на то, какая редакция была последней действующей, см. VndService.CancelAsync).
    private static VndRedactionResponse ToRedactionResponse(VndRedaction x, VndDocument vnd) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Number = x.Number,
        Description = x.Description,
        IsCurrent = x.Id == vnd.CurrentRedactionId && vnd.Status != VndStatus.Archived,
        DocFileRuId = x.DocFileRuId,
        DocFileKgId = x.DocFileKgId,
        DocFileEnId = x.DocFileEnId,
        DocRuUpdatedAt = x.DocRuUpdatedAt,
        DocKgUpdatedAt = x.DocKgUpdatedAt,
        DocEnUpdatedAt = x.DocEnUpdatedAt,
        TidFileId = x.TidFileId,
        ApprovalSheetFileId = x.ApprovalSheetFileId,
        DisagreementMatrixFileId = x.DisagreementMatrixFileId,
        RequiresApproval = x.RequiresApproval,
        ApprovalStatus = x.ApprovalStatus.ToString(),
        AttachmentFileIds = x.Attachments.Select(a => a.FileAttachmentId).ToList(),
        Attachments = x.Attachments.Select(a => new VndRedactionAttachmentResponse
        {
            FileId = a.FileAttachmentId,
            FileName = a.FileAttachment?.OriginalFileName ?? $"Вложение_{a.FileAttachmentId}",
            SizeBytes = a.FileAttachment?.SizeBytes ?? 0
        }).ToList(),
        TitleRu = x.TitleRu,
        TitleEn = x.TitleEn,
        TitleKg = x.TitleKg,
        TypeId = x.TypeId,
        TypeName = x.Type?.TitleRu ?? "",
        AdoptionDate = x.AdoptionDate,
        AdoptionCode = x.AdoptionCode,
        EffectiveDate = x.EffectiveDate,
        Period = x.Period.ToString(),
        DeveloperId = x.DeveloperId,
        DeveloperName = x.Developer?.TitleRu ?? "",
        CuratorDeveloperId = x.CuratorDeveloperId,
        CuratorDeveloperName = x.CuratorDeveloper?.FullName,
        OrganId = x.OrganId,
        OrganName = x.Organ?.TitleRu ?? "",
        SecrecyLevelId = x.SecrecyLevelId,
        ResponsibleExecutorIds = x.ResponsibleExecutors.Select(e => e.Id).ToList(),
        KeywordIds = x.Keywords.Select(k => k.Id).ToList(),
        RubricIds = x.Rubrics.Select(r => r.Id).ToList(),
        CreatedAt = x.CreatedAt
    };

    // Создание черновика ВНД
    public async Task<VndResponse> CreateAsync(CreateVndRequest request, int currentUserId, string languageCode)
    {
        if (!_currentUser.HasPermission(PermissionCode.CreateVndWithApproval) &&
            !_currentUser.HasPermission(PermissionCode.CreateVndWithoutApproval))
            throw new UnauthorizedAccessException("У вас нет прав создавать новые ВНД!");

        var typeExists = await _db.TypesVnd.AnyAsync(x => x.Id == request.TypeId);
        if (!typeExists) throw new KeyNotFoundException($"Вид ВНД с id={request.TypeId} не найден");

        var organExists = await _db.ApprovalBodies.AnyAsync(x => x.Id == request.OrganId);
        if (!organExists) throw new KeyNotFoundException($"Орган утверждения с id={request.OrganId} не найден");

        var currentUser = await _db.Users.FindAsync(currentUserId)
                          ?? throw new KeyNotFoundException("Текущий пользователь не найден");

        var developerId = request.DeveloperId ?? currentUser.OrgUnitId
            ?? throw new InvalidOperationException(
                "Не указан разработчик (СП), и у текущего пользователя не назначено подразделение");

        var developer = await _db.OrganizationUnits.FindAsync(developerId)
                        ?? throw new KeyNotFoundException($"Структурное подразделение с id={developerId} не найдено");

        var curatorDeveloperId = request.CuratorDeveloperId ?? developer.CuratorUserId;
        if (curatorDeveloperId.HasValue)
        {
            var curatorExists = await _db.Users.AnyAsync(x => x.Id == curatorDeveloperId.Value);
            if (!curatorExists) throw new KeyNotFoundException($"Куратор с id={curatorDeveloperId} не найден");
        }

        var responsibleExecutorIds = request.ResponsibleExecutorIds.Count > 0
            ? request.ResponsibleExecutorIds
            : [developerId];

        var responsibleExecutors = await _db.OrganizationUnits
            .Where(x => responsibleExecutorIds.Contains(x.Id)).ToListAsync();
        var missingExecutors = responsibleExecutorIds.Except(responsibleExecutors.Select(x => x.Id)).ToList();
        if (missingExecutors.Count > 0)
            throw new KeyNotFoundException($"Подразделения с id={string.Join(", ", missingExecutors)} не найдены");

        var keywords = await GetByIdsAsync(_db.Keywords, request.KeywordIds, "Ключевые слова");
        var rubrics = await GetByIdsAsync(_db.Rubrics, request.RubricIds, "Рубрики");
        var userGroups = await GetByIdsAsync(_db.UserGroups, request.UserGroupIds, "Группы пользователей");

        if (request.SecrecyLevelId.HasValue)
        {
            var exists = await _db.SecurityLevels.AnyAsync(x => x.Id == request.SecrecyLevelId.Value);
            if (!exists) throw new KeyNotFoundException($"Уровень секретности с id={request.SecrecyLevelId} не найден");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = ResolveDueDate(request.Period, request.DueActualizationDate, today);

        var entity = new VndDocument
        {
            Code = await GenerateNextCodeAsync(),
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg,
            Status = VndStatus.Draft,
            TypeId = request.TypeId,
            DeveloperId = developerId,
            CuratorDeveloperId = curatorDeveloperId,
            OrganId = request.OrganId,
            ResponsibleExecutors = responsibleExecutors,
            Keywords = keywords,
            Rubrics = rubrics,
            UserGroups = userGroups,
            SecrecyLevelId = request.SecrecyLevelId ?? 1, // дефолт "Открытый доступ" 
            Period = request.Period,
            LastActualizationDate = today,
            DueActualizationDate = dueDate,
            LastActualizationHadChanges = false,
            CreatedByUserId = currentUserId
        };

        _db.VndDocuments.Add(entity);
        await _db.SaveChangesAsync();

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Created, entity.Id, entity.Code,
            currentUserId,
            new ActivityText(
                $"{currentUser.FullName} создал(а) черновик нового ВНД {entity.Code} «{entity.TitleRu}»",
                $"{currentUser.FullName} created draft VND {entity.Code} \"{entity.TitleRu}\"",
                $"{currentUser.FullName} {entity.Code} «{entity.TitleRu}» черновик ВНДди түздү"),
            $"/base-vnd/{entity.Id}");
        await _db.SaveChangesAsync();

        var canViewExtended = _currentUser.HasPermission(PermissionCode.ViewVndRegistryExtended);
        return ToResponse(entity, languageCode, today, canViewExtended);
    }

    private static DateOnly ResolveDueDate(ActualizationPeriod period, DateOnly? customDate, DateOnly today) =>
        period switch
        {
            ActualizationPeriod.Custom => customDate
                                          ?? throw new InvalidOperationException(
                                              "Для периода Custom необходимо указать DueActualizationDate"),
            ActualizationPeriod.Quarterly => today.AddMonths(3),
            ActualizationPeriod.HalfYear => today.AddMonths(6),
            ActualizationPeriod.Annual => today.AddMonths(12),
            ActualizationPeriod.Biennial => today.AddMonths(24),
            ActualizationPeriod.Triennial => today.AddMonths(36),
            _ => throw new InvalidOperationException("Неизвестный период актуализации")
        };

    private async Task<string> GenerateNextCodeAsync()
    {
        const int startingNumber = 10210;

        var maxExisting = await _db.VndDocuments
            .Select(x => x.Code)
            .ToListAsync(); // коды хранятся строкой — парсим на стороне клиента

        var maxNum = maxExisting
            .Select(c => int.TryParse(c, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return (Math.Max(maxNum, startingNumber - 1) + 1).ToString();
    }

    private async Task<List<T>> GetByIdsAsync<T>(DbSet<T> set, List<int> ids, string entityName) where T : class
    {
        if (ids.Count == 0) return [];
        var items = await set.Where(x => ids.Contains(EF.Property<int>(x, "Id"))).ToListAsync();
        if (items.Count != ids.Distinct().Count())
            throw new KeyNotFoundException($"{entityName}: не все id найдены");
        return items;
    }

    /// <summary>Причастен ли пользователь к документу: разработчик (через куратора), куратор,
    /// ответственный исполнитель (через куратора подразделения), инициатор/создатель,
    /// ответственный за текущую актуализацию, либо участник процесса согласования
    /// (инициатор или согласующий на одном из этапов, в любом из процессов документа).</summary>
    private async Task<bool> IsLinkedToVndAsync(VndDocument vnd, int currentUserId)
    {
        if (vnd.CreatedByUserId == currentUserId) return true;
        if (vnd.CuratorDeveloperId == currentUserId) return true;
        if (vnd.ActualizationResponsibleUserId == currentUserId) return true;

        var isResponsibleExecutorCurator = await _db.Entry(vnd)
            .Collection(x => x.ResponsibleExecutors)
            .Query()
            .AnyAsync(e => e.CuratorUserId == currentUserId);
        if (isResponsibleExecutorCurator) return true;

        return await _db.VndApprovalProcesses
            .Where(p => p.VndId == vnd.Id)
            .AnyAsync(p => p.InitiatorUserId == currentUserId
                           || p.Stages.Any(s => s.ApproverUserId == currentUserId));
    }

    /*
    Правила при загрузке новой редакции:
    1. Первая редакция документа, RequiresApproval = false - сразу становится актуальной.
    2. Первая редакция документа, RequiresApproval = true - создаётся, но актуальной
     не становится; ждёт согласования.
    3. Есть предыдущие редакции, новая с RequiresApproval = false - сразу
     становится актуальной, прежняя актуальная автоматически "теряет" этот статус (вытеснена).
    4. Есть предыдущие редакции, новая с RequiresApproval = true - создаётся,
     но актуальной не становится; прежняя актуальная остаётся актуальной до исхода согласования.
    5. Блокировка загрузки: если последняя по номеру редакция документа находится
     в статусе "на согласовании" (Pending) — новую редакцию загрузить нельзя. Сначала нужно её согласовать/отклонить/отозвать.
    */

    public async Task<VndRedactionResponse> AddRedactionAsync(
        int vndId, CreateVndRedactionRequest request, int currentUserId)
    {
        // Include-ы ниже (Developer/CuratorDeveloper/Organ/SecrecyLevel/ResponsibleExecutors/
        // Rubrics/Keywords) нужны только на случай, если это ПЕРВАЯ редакция документа — тогда
        // реквизиты новой редакции наследуются с самого VndDocument (см. requisitesSource ниже).
        var vnd = await _db.VndDocuments
                      .Include(x => x.Type)
                      .Include(x => x.Developer)
                      .Include(x => x.CuratorDeveloper)
                      .Include(x => x.Organ)
                      .Include(x => x.SecrecyLevel)
                      .Include(x => x.ResponsibleExecutors)
                      .Include(x => x.Rubrics)
                      .Include(x => x.Keywords)
                      .FirstOrDefaultAsync(x => x.Id == vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (!IsChiefEditor() && !await IsLinkedToVndAsync(vnd, currentUserId))
            throw new UnauthorizedAccessException(
                "Загружать новую редакцию может только разработчик, куратор, ответственный исполнитель, " +
                "инициатор, ответственный за актуализацию или главный редактор ВНД");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        // Правило: последняя редакция не должна быть незавершённой (черновик или на согласовании)
        var lastRedaction = await IncludeRequisites(_db.VndRedactions.Where(r => r.VndId == vndId))
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync();

        if (lastRedaction is not null &&
            (lastRedaction.ApprovalStatus == RedactionApprovalStatus.Draft ||
             lastRedaction.ApprovalStatus == RedactionApprovalStatus.Pending))
        {
            var reason = lastRedaction.ApprovalStatus == RedactionApprovalStatus.Draft
                ? "ещё не отправлена на согласование"
                : "ожидает решения по согласованию";
            throw new InvalidOperationException(
                $"Редакция {lastRedaction.Code} {reason}. Завершите работу с ней, прежде чем загружать новую.");
        }

        // ТИД больше не требуется прямо при загрузке редакции (даже при актуализации) — его можно
        // приложить отдельным шагом позже, кнопкой "Сформировать или загрузить ТИД" на странице
        // ВНД (см. UploadTidForLastRedactionAsync ниже). Но отправить такую редакцию на
        // согласование или опубликовать без согласования без ТИД всё ещё нельзя — это
        // проверяется в VndApprovalService.StartAsync и PublishRedactionWithoutApprovalAsync.

        // Вторую и последующие редакции можно добавлять только в рамках открытого цикла
        // актуализации (см. VndActualizationService.StartAsync/ConfirmStartAfterRequestAsync) —
        // без этого документ должен оставаться на действующей редакции до тех пор, пока кто-то
        // не возьмёт его в актуализацию. Первая редакция нового ВНД (lastRedaction == null)
        // этим правилом не ограничена.
        if (lastRedaction is not null && vnd.Status != VndStatus.OnActualization)
            throw new InvalidOperationException(
                "Добавить новую редакцию действующего ВНД можно только в рамках актуализации — " +
                "начните актуализацию во вкладке «Актуализация»");

        // Внутри цикла актуализации новую редакцию можно грузить только после того, как
        // ответственный зафиксировал финальные сдвиг срока/"без изменений" на шаге
        // "Выполнить актуализацию" (см. VndDocument.ActualizationPerformed,
        // VndActualizationService.PerformAsync/ConfirmStartAfterRequestAsync) — до этого момента
        // решение о самой стратегии цикла ещё не принято.
        if (lastRedaction is not null && vnd.Status == VndStatus.OnActualization && !vnd.ActualizationPerformed)
            throw new InvalidOperationException(
                "Прежде чем загружать новую редакцию, выполните шаг «Выполнить актуализацию»");

        // В рамках открытого цикла актуализации решение "с согласованием / без" уже зафиксировано
        // при старте цикла (StartAsync/ConfirmStartAfterRequestAsync) — не доверяем тому, что
        // прислал клиент в request.RequiresApproval, иначе обычный редактор без прав на
        // актуализацию без согласования мог бы обойти это ограничение, отредактировав запрос
        // напрямую.
        // Вне цикла актуализации (первая редакция нового ВНД) решение в обычном случае остаётся
        // за тем, кто загружает — но опубликовать её сразу действующей, без согласования
        // (RequiresApproval = false), может только тот, у кого есть право
        // CreateVndWithoutApproval (сейчас — главный редактор и администратор); иначе, даже если
        // клиент прислал RequiresApproval = false (напрямую отредактировав запрос, минуя
        // скрытый на фронте чекбокс — см. canSkipApproval в VndUploadRedactionModal), редакция
        // всё равно уходит на согласование.
        var effectiveRequiresApproval = vnd.Status == VndStatus.OnActualization
            ? vnd.ActualizationRequiresApproval
            : request.RequiresApproval || !CanPublishWithoutApproval();

        // Раз загружается настоящая новая редакция — план "актуализация без изменений" (если он
        // был) больше не в силе: изменения всё-таки есть.
        if (vnd.ActualizationPlannedNoChanges)
            vnd.ActualizationPlannedNoChanges = false;

        var docRu = await _fileService.SaveAsync(request.DocRu, currentUserId);
        var docKg = request.DocKg is not null ? await _fileService.SaveAsync(request.DocKg, currentUserId) : null;
        var docEn = request.DocEn is not null ? await _fileService.SaveAsync(request.DocEn, currentUserId) : null;
        var tid = request.Tid is not null ? await _fileService.SaveAsync(request.Tid, currentUserId) : null;

        var attachmentEntities = await BuildAttachmentEntitiesAsync(vndId, request, currentUserId);

        var nextNumber = (lastRedaction?.Number ?? 0) + 1;

        // Актуализационная редакция (Number > 1) без ТИД не может стать текущей и утащить ВНД в
        // консолидацию сразу, даже если согласование не требуется — иначе документ выглядел бы
        // уже "актуальным"/"в консолидации", хотя обязательный ТИД ещё не приложен (а приложить
        // его после такого мгновенного перехода было негде — см. UploadTidForLastRedactionAsync,
        // которая работает только с редакцией в статусе "черновик"). Поэтому если ТИД не был
        // передан прямо в этом запросе, редакция временно остаётся черновиком (ApprovalStatus.Draft)
        // несмотря на RequiresApproval = false — сам переход "стать текущей/уйти в консолидацию"
        // довыполнится в UploadTidForLastRedactionAsync, как только ТИД будет приложен.
        var blockedByMissingTid = !effectiveRequiresApproval && nextNumber > 1 && tid is null;

        // Реквизиты новой редакции ("Реквизиты" → вкладка Р{N}) стартуют как копия реквизитов
        // предыдущей редакции (а для самой первой редакции — реквизитов, заданных при создании
        // ВНД, см. CreateAsync) — дальше их можно скорректировать через UpdateRequisitesAsync,
        // указав RedactionId именно этой редакции. Дату/номер утверждения и дату вступления в
        // силу НЕ наследуем — они всегда null для новой, ещё не утверждённой редакции: заполнятся
        // при консолидации (см. VndActualizationService.PublishAsync).
        var redaction = new VndRedaction
        {
            VndId = vndId,
            Number = nextNumber,
            Code = $"{vnd.Code}-Р{nextNumber}",
            Description = request.Description,
            DocFileRuId = docRu.Id,
            DocFileKgId = docKg?.Id,
            DocFileEnId = docEn?.Id,
            TidFileId = tid?.Id,
            RequiresApproval = effectiveRequiresApproval,
            ApprovalStatus = (effectiveRequiresApproval || blockedByMissingTid)
                ? RedactionApprovalStatus.Draft
                : RedactionApprovalStatus.NotRequired,
            Attachments = attachmentEntities,
            TitleRu = "" // временно, ниже сразу перезатирается CopyRequisitesFrom
        };
        CopyRequisitesFrom(redaction, lastRedaction, vnd);

        _db.VndRedactions.Add(redaction);
        await _db.SaveChangesAsync();

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.ItemAdded, vndId, vnd.Code,
            currentUserId,
            new ActivityText(
                $"{actorName} добавил(а) редакцию {redaction.Code} ВНД «{vnd.TitleRu}»",
                $"{actorName} added revision {redaction.Code} of VND \"{vnd.TitleRu}\"",
                $"{actorName} «{vnd.TitleRu}» ВНДисине {redaction.Code} редакциясын кошту"),
            $"/base-vnd/{vndId}");
        await _db.SaveChangesAsync();

        if (!effectiveRequiresApproval && !blockedByMissingTid)
        {
            vnd.CurrentRedactionId = redaction.Id;
            vnd.RevisionChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Если документ был в цикле актуализации - консолидация обязательна,
            // даже если конкретно эта редакция не требовала согласования.
            // Иначе (первая редакция нового ВНД, или обычное обновление активного
            // документа без согласования, без консолидации) - сразу становится действующим, как раньше.
            var enteringConsolidation = vnd.Status == VndStatus.OnActualization;
            vnd.Status = enteringConsolidation ? VndStatus.Consolidation : VndStatus.Active;

            if (enteringConsolidation)
                await StampConsolidationStartedAsync(vndId);

            await _db.SaveChangesAsync();
        }

        return ToRedactionResponse(redaction, vnd);
    }

    /// <summary>
    /// Собирает вложения новой редакции из двух источников:
    /// - request.ExistingAttachmentFileIds — файлы, перенесённые "как есть" из предыдущих редакций
    ///   этого же ВНД (см. блок "Вложения" в VndUploadRedactionModal, предзаполненный вложениями
    ///   последней редакции) - без повторной загрузки, просто новая привязка к тому же FileAttachmentId;
    /// - request.Attachments — новые файлы, которые пользователь выбрал через проводник. Каждый
    ///   такой файл ПЕРЕД загрузкой в MinIO сверяется по SHA-256 с файлами, уже приложенными к
    ///   какой-либо редакции ЭТОГО ВНД (включая только что перенесённые/загруженные в этом же
    ///   запросе) — если содержимое совпадает, файл не грузится повторно, переиспользуется
    ///   существующий FileAttachmentId. Сверка нарочно ограничена этим ВНД (не всей системой) —
    ///   вложение может использоваться в другом, не связанном ВНД, и должно спокойно исчезать при
    ///   удалении текущего документа (см. DeleteAsync ниже), не задевая чужие ссылки.
    /// </summary>
    private async Task<List<VndRedactionAttachment>> BuildAttachmentEntitiesAsync(
        int vndId, CreateVndRedactionRequest request, int currentUserId)
    {
        // Пул "кандидатов на переиспользование" — все файлы вложений, когда-либо приложенные
        // к редакциям этого ВНД. FileAttachment подгружаем сразу (Include), он нужен и для
        // сверки по хешу, и для навигации в новых VndRedactionAttachment (см. комментарий ниже).
        var candidateAttachments = await _db.Set<VndRedactionAttachment>()
            .Where(a => a.VndRedaction!.VndId == vndId)
            .Include(a => a.FileAttachment)
            .Select(a => a.FileAttachment!)
            .ToListAsync();
        // Один и тот же файл может встречаться у нескольких редакций этого ВНД - дедуп по id
        // делаем на стороне .NET (после выборки), чтобы не полагаться на трансляцию Distinct()
        // по entity-типу в SQL.
        var candidatesById = candidateAttachments
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => g.First());
        var candidates = candidatesById.Values.ToList();

        var attachmentEntities = new List<VndRedactionAttachment>();

        // Перенесённые без изменений вложения предыдущей редакции - только те id, что
        // действительно принадлежат этому ВНД (см. XML-комментарий у ExistingAttachmentFileIds).
        // Один и тот же id мог быть прислан дважды (например, повторный клик) - Distinct на входе.
        foreach (var fileId in (request.ExistingAttachmentFileIds ?? []).Distinct())
        {
            if (candidatesById.TryGetValue(fileId, out var existing))
                attachmentEntities.Add(new VndRedactionAttachment {FileAttachmentId = existing.Id, FileAttachment = existing});
        }

        foreach (var file in request.Attachments ?? [])
        {
            var hash = await _fileService.ComputeHashAsync(file);
            // Ищем среди кандидатов ЭТОГО ВНД (включая уже перенесённые/только что загруженные
            // выше в этом же цикле - candidates пополняется ниже при реальной загрузке) файл
            // с тем же содержимым - совпадение по хешу и размеру.
            var duplicate = candidates.FirstOrDefault(f => f.Hash == hash && f.SizeBytes == file.Length);

            if (duplicate is not null)
            {
                attachmentEntities.Add(new VndRedactionAttachment {FileAttachmentId = duplicate.Id, FileAttachment = duplicate});
                continue;
            }

            var saved = await _fileService.SaveAsync(file, currentUserId);
            // FileAttachment = saved заполняет навигацию сразу в памяти (без лишнего запроса к
            // БД) — нужно, чтобы ToRedactionResponse ниже сразу получил оригинальное имя файла.
            attachmentEntities.Add(new VndRedactionAttachment {FileAttachmentId = saved.Id, FileAttachment = saved});
            // Пополняем пул кандидатов - если следующий файл в этом же запросе побайтово
            // совпадёт с только что загруженным, он тоже переиспользует его вместо повторной загрузки.
            candidates.Add(saved);
        }

        return attachmentEntities;
    }

    // Отправка редакции на согласование: переводит черновик редакции в Pending и ВНД в Review.
    public async Task<VndRedactionResponse> SubmitRedactionForApprovalAsync(
        int vndId, int redactionId, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (!IsChiefEditor() && !await IsLinkedToVndAsync(vnd, currentUserId))
            throw new UnauthorizedAccessException(
                "Отправить редакцию на согласование может только причастный к этому ВНД пользователь");

        var redaction = await IncludeRequisites(_db.VndRedactions
                                .Include(x => x.Attachments).ThenInclude(a => a.FileAttachment))
                            .FirstOrDefaultAsync(x => x.Id == redactionId && x.VndId == vndId)
                        ?? throw new KeyNotFoundException($"Редакция с id={redactionId} не найдена");

        if (redaction.ApprovalStatus != RedactionApprovalStatus.Draft)
            throw new InvalidOperationException("Отправить на согласование можно только черновик редакции");

        redaction.ApprovalStatus = RedactionApprovalStatus.Pending;
        vnd.Status = VndStatus.Review;
        await _db.SaveChangesAsync();

        return ToRedactionResponse(redaction, vnd);
    }

    /// <summary>Только для главного редактора: делает черновик редакции действующим/текущим
    /// НАПРЯМУЮ, минуя весь процесс согласования целиком - тот же результат, что и загрузка
    /// редакции без согласования (см. ветку RequiresApproval=false в UploadRedactionAsync выше),
    /// только применительно к уже загруженному черновику, который иначе пошёл бы по обычному
    /// пути согласования. Рядом с обычной кнопкой "Отправить на согласование" - альтернатива
    /// для случаев, когда главный редактор явно берёт ответственность на себя.</summary>
    public async Task<VndRedactionResponse> PublishRedactionWithoutApprovalAsync(
        int vndId, int redactionId, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (!CanPublishWithoutApproval())
            throw new UnauthorizedAccessException(
                "Сделать редакцию действующей без согласования может только главный редактор");

        var redaction = await IncludeRequisites(_db.VndRedactions
                                .Include(x => x.Attachments).ThenInclude(a => a.FileAttachment))
                            .FirstOrDefaultAsync(x => x.Id == redactionId && x.VndId == vndId)
                        ?? throw new KeyNotFoundException($"Редакция с id={redactionId} не найдена");

        if (redaction.ApprovalStatus != RedactionApprovalStatus.Draft)
            throw new InvalidOperationException(
                "Сделать действующей без согласования можно только черновик редакции");

        // Актуализационная редакция (Number > 1) не может миновать согласование без ТИД —
        // раньше это проверялось при самой загрузке (AddRedactionAsync), теперь ТИД
        // прикладывается отдельным шагом, поэтому проверка переехала сюда и в StartAsync.
        if (redaction.Number > 1 && redaction.TidFileId is null)
            throw new InvalidOperationException(
                "Прежде чем сделать редакцию действующей без согласования, приложите файл ТИД " +
                "(Таблица изменений и дополнений) — кнопка «Сформировать или загрузить ТИД»");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";

        redaction.RequiresApproval = false;
        redaction.ApprovalStatus = RedactionApprovalStatus.NotRequired;

        vnd.CurrentRedactionId = redaction.Id;
        vnd.RevisionChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Если документ был в цикле актуализации - консолидация обязательна, даже если сама
        // редакция обошлась без согласования (см. тот же принцип в UploadRedactionAsync).
        var enteringConsolidation = vnd.Status == VndStatus.OnActualization;
        vnd.Status = enteringConsolidation ? VndStatus.Consolidation : VndStatus.Active;

        if (enteringConsolidation)
            await StampConsolidationStartedAsync(vndId);

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Other, vndId, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} сделал(а) редакцию {redaction.Code} ВНД «{vnd.TitleRu}» действующей " +
                "без согласования (главный редактор)",
                $"{actorName} made revision {redaction.Code} of VND \"{vnd.TitleRu}\" active " +
                "without approval (chief editor)",
                $"{actorName} «{vnd.TitleRu}» ВНДисинин {redaction.Code} редакциясын макулдашуусуз " +
                "колдонуудагы кылды (башкы редактор)"),
            $"/base-vnd/{vndId}");

        await _db.SaveChangesAsync();

        return ToRedactionResponse(redaction, vnd);
    }


    public async Task<List<VndRedactionResponse>> GetRedactionsAsync(int vndId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var redactions = await IncludeRequisites(_db.VndRedactions
                .Where(x => x.VndId == vndId)
                .Include(x => x.Attachments).ThenInclude(a => a.FileAttachment))
            .OrderBy(x => x.Number)
            .ToListAsync();

        return redactions.Select(r => ToRedactionResponse(r, vnd)).ToList();
    }

    /// <summary>
    /// Удаление ВНД. Разрешено только для черновика (действующие/архивные/на согласовании
    /// удалять нельзя) и только создателю или главному редактору. Явно чистит связанные
    /// редакции, вложения, ссылки, процессы согласования и файлы в хранилище.
    /// </summary>
    public async Task DeleteAsync(int id, int currentUserId)
    {
        var vnd = await _db.VndDocuments
                      .Include(x => x.Redactions).ThenInclude(r => r.Attachments)
                      .FirstOrDefaultAsync(x => x.Id == id)
                  ?? throw new KeyNotFoundException($"ВНД с id={id} не найден");

        if (vnd.Status != VndStatus.Draft)
            throw new InvalidOperationException("Удалить можно только ВНД в статусе черновика");

        if (vnd.CreatedByUserId != currentUserId && !IsChiefEditor())
            throw new UnauthorizedAccessException("Удалить ВНД может только его создатель или главный редактор");

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";
        var vndCode = vnd.Code;
        var vndTitle = vnd.TitleRu;

        // Собираем id файлов редакций для последующего удаления из хранилища.
        var fileIds = new List<int>();
        foreach (var r in vnd.Redactions)
        {
            fileIds.Add(r.DocFileRuId);
            if (r.DocFileKgId.HasValue) fileIds.Add(r.DocFileKgId.Value);
            if (r.DocFileEnId.HasValue) fileIds.Add(r.DocFileEnId.Value);
            if (r.TidFileId.HasValue) fileIds.Add(r.TidFileId.Value);
            fileIds.AddRange(r.Attachments.Select(a => a.FileAttachmentId));
        }

        var links = await _db.Set<VndLink>()
            .Where(l => l.SourceVndId == id || l.TargetVndId == id)
            .ToListAsync();
        _db.Set<VndLink>().RemoveRange(links);

        var processes = await _db.VndApprovalProcesses
            .Where(p => p.VndId == id)
            .Include(p => p.Stages)
            .Include(p => p.DisagreementMatrixRows)
            .ToListAsync();
        _db.VndApprovalProcesses.RemoveRange(processes);

        foreach (var r in vnd.Redactions)
            _db.Set<VndRedactionAttachment>().RemoveRange(r.Attachments);
        _db.VndRedactions.RemoveRange(vnd.Redactions);
        _db.VndDocuments.Remove(vnd);
        await _db.SaveChangesAsync();

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Other, id, vndCode,
            currentUserId,
            new ActivityText(
                $"{actorName} удалил(а) черновик ВНД {vndCode} «{vndTitle}»",
                $"{actorName} deleted draft VND {vndCode} \"{vndTitle}\"",
                $"{actorName} {vndCode} «{vndTitle}» ВНДисинин черновигин өчүрдү"),
            null);

        // Файлы удаляем после метаданных, best-effort — недоступность хранилища не должна
        // откатывать уже выполненное удаление документа.
        foreach (var fileId in fileIds.Distinct())
        {
            try { await _fileService.DeleteAsync(fileId); }
            catch { /* файл уже удалён или хранилище недоступно — не критично */ }
        }
    }

    /// <summary>
    /// Архивировать (отменить) ВНД — кнопка "Архивировать" (см. PermissionCode.CancelVnd).
    /// Доступно на любом статусе, кроме черновика (тот только удаляется, см. DeleteAsync) и
    /// уже архивированного. № и дата отмены обязательны — отмена оформляется служебной запиской
    /// и не согласуется.
    ///
    /// Если документ на момент архивации "На согласовании" — сначала отзываем согласование тем
    /// же путём, что и обычный отзыв (VndApprovalService.WithdrawForCancelAsync), только без
    /// требования к архивирующему быть инициатором или иметь отдельно CancelAnyVndApproval:
    /// право CancelVnd на саму архивацию тут уже достаточное основание. Открытый цикл
    /// актуализации (если он был — на OnActualization/Consolidation) закрываем так же, как при
    /// обычной публикации (см. VndActualizationService.PublishAsync) — архивный документ не
    /// может "ждать" ответственного за актуализацию.
    /// </summary>
    public async Task<VndResponse> CancelAsync(
        int id, CancelVndRequest request, int currentUserId, string languageCode)
    {
        var vnd = await _db.VndDocuments
                      .Include(x => x.Type)
                      .Include(x => x.Developer)
                      .Include(x => x.CuratorDeveloper)
                      .Include(x => x.Organ)
                      .Include(x => x.ResponsibleExecutors)
                      .Include(x => x.Rubrics)
                      .Include(x => x.Keywords)
                      .Include(x => x.UserGroups)
                      .Include(x => x.Redactions)
                      .Include(x => x.CreatedByUser)
                      .Include(x => x.ActualizationResponsibleUser)
                      .FirstOrDefaultAsync(x => x.Id == id)
                  ?? throw new KeyNotFoundException($"ВНД с id={id} не найден");

        if (vnd.Status is VndStatus.Draft or VndStatus.Archived)
            throw new InvalidOperationException(
                "Архивировать нельзя черновик (его можно только удалить) или уже архивированный документ");

        if (string.IsNullOrWhiteSpace(request.CancelCode))
            throw new InvalidOperationException("Укажите № отмены");

        // Согласование отзываем ДО того, как перезапишем Status на Archived - иначе
        // WithdrawForCancelAsync (который сам определяет процесс по текущему статусу редакции)
        // не найдёт, что отзывать.
        if (vnd.Status == VndStatus.Review)
            await _approvalService.WithdrawForCancelAsync(id, currentUserId);

        var actor = await _db.Users.FindAsync(currentUserId);
        var actorName = actor?.FullName ?? "—";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        vnd.Status = VndStatus.Archived;
        vnd.CancelCode = request.CancelCode;
        vnd.CancelDate = request.CancelDate;
        vnd.CancelReason = request.CancelReason;
        vnd.ArchivedDate = today;

        // Закрываем открытый цикл актуализации, если он был - как и после обычной публикации
        // (VndActualizationService.PublishAsync), только без публикации.
        vnd.ActualizationResponsibleUserId = null;
        vnd.ActualizationRequiresApproval = false;
        vnd.ActualizationShiftNextPeriod = false;
        vnd.ActualizationPlannedNoChanges = false;
        vnd.ActualizationPerformed = false;

        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.Other, id, vnd.Code, currentUserId,
            new ActivityText(
                $"{actorName} архивировал(а) ВНД {vnd.Code} «{vnd.TitleRu}»",
                $"{actorName} archived VND {vnd.Code} \"{vnd.TitleRu}\"",
                $"{actorName} {vnd.Code} «{vnd.TitleRu}» ВНДисин архивдеди"),
            $"/base-vnd/{id}");

        await _db.SaveChangesAsync();

        var canViewExtended = _currentUser.HasPermission(PermissionCode.ViewVndRegistryExtended);
        return ToResponse(vnd, languageCode, today, canViewExtended);
    }

    /// <summary>Обновляет реквизиты ВНД. TitleRu/En/Kg, TypeId, утверждение/вступление в силу,
    /// разработчик/куратор/орган, гриф секретности, ответственные исполнители, рубрики, ключевые
    /// слова — всё это теперь принадлежит КОНКРЕТНОЙ редакции (см. миграцию "реквизиты по
    /// редакции", включая заголовок/вид) — request.RedactionId указывает, какой именно (вкладки
    /// Р1/Р2/.../Рn на вкладке "Реквизиты"); если не указано — берётся текущая/последняя
    /// редакция. Общими на весь документ остаются только служебные даты цикла актуализации
    /// (DueActualizationDate/LastActualizationDate), отмена/архивация и группы доступа.
    /// Пока не убран старый дублирующий набор полей на VndDocument (переходный период —
    /// см. пометку в VndDocument.cs), при редактировании ИМЕННО текущей редакции те же
    /// значения зеркалируются и туда, чтобы не сломать существующий поиск/фильтры по документу.</summary>
    public async Task<VndResponse> UpdateRequisitesAsync(int id, UpdateVndRequisitesRequest request,
        string languageCode)
    {
        var entity = await _db.VndDocuments
                         .Include(x => x.Type)
                         .Include(x => x.UserGroups)
                         .Include(x => x.Redactions)
                         .Include(x => x.CreatedByUser)
                         .Include(x => x.ActualizationResponsibleUser)
                         .FirstOrDefaultAsync(x => x.Id == id)
                     ?? throw new KeyNotFoundException($"ВНД с id={id} не найден");

        var targetRedactionId = request.RedactionId ?? entity.CurrentRedactionId
            ?? entity.Redactions.OrderByDescending(r => r.Number).Select(r => (int?)r.Id).FirstOrDefault();

        var targetRedaction = targetRedactionId.HasValue
            ? await IncludeRequisites(_db.VndRedactions)
                  .FirstOrDefaultAsync(r => r.Id == targetRedactionId.Value && r.VndId == id)
              ?? throw new KeyNotFoundException($"Редакция с id={targetRedactionId} не найдена")
            : null;

        var typeExists = await _db.TypesVnd.AnyAsync(x => x.Id == request.TypeId);
        if (!typeExists) throw new KeyNotFoundException($"Вид ВНД с id={request.TypeId} не найден");

        var organExists = await _db.ApprovalBodies.AnyAsync(x => x.Id == request.OrganId);
        if (!organExists) throw new KeyNotFoundException($"Орган утверждения с id={request.OrganId} не найден");

        var fallbackDeveloperId = targetRedaction?.DeveloperId ?? entity.DeveloperId;

        int developerId;
        if (request.DeveloperId.HasValue)
        {
            var developerExists = await _db.OrganizationUnits.AnyAsync(x => x.Id == request.DeveloperId.Value);
            if (!developerExists)
                throw new KeyNotFoundException($"Структурное подразделение с id={request.DeveloperId} не найдено");
            developerId = request.DeveloperId.Value;
        }
        else
        {
            developerId = fallbackDeveloperId; // не меняем, если не передали
        }

        if (request.CuratorDeveloperId.HasValue)
        {
            var curatorExists = await _db.Users.AnyAsync(x => x.Id == request.CuratorDeveloperId.Value);
            if (!curatorExists)
                throw new KeyNotFoundException($"Куратор с id={request.CuratorDeveloperId} не найден");
        }

        var responsibleExecutorIds = request.ResponsibleExecutorIds.Count > 0
            ? request.ResponsibleExecutorIds
            : [developerId];

        var responsibleExecutors = await _db.OrganizationUnits
            .Where(x => responsibleExecutorIds.Contains(x.Id)).ToListAsync();
        var missingExecutors = responsibleExecutorIds.Except(responsibleExecutors.Select(x => x.Id)).ToList();
        if (missingExecutors.Count > 0)
            throw new KeyNotFoundException($"Подразделения с id={string.Join(", ", missingExecutors)} не найдены");

        var keywords = await GetByIdsAsync(_db.Keywords, request.KeywordIds, "Ключевые слова");
        var rubrics = await GetByIdsAsync(_db.Rubrics, request.RubricIds, "Рубрики");
        var userGroups = await GetByIdsAsync(_db.UserGroups, request.UserGroupIds, "Группы пользователей");

        if (request.SecrecyLevelId.HasValue)
        {
            var secrecyExists = await _db.SecurityLevels.AnyAsync(x => x.Id == request.SecrecyLevelId.Value);
            if (!secrecyExists)
                throw new KeyNotFoundException($"Уровень секретности с id={request.SecrecyLevelId} не найден");
        }

        // --- Общие на весь документ (не зависят от редакции) ---
        entity.DueActualizationDate = request.DueActualizationDate;
        entity.LastActualizationDate = request.LastActualizationDate;
        entity.LastActualizationHadChanges = request.LastActualizationHadChanges;

        entity.CancelDate = request.CancelDate;
        entity.CancelCode = request.CancelCode;
        entity.CancelReason = request.CancelReason;
        entity.ArchivedDate = request.ArchivedDate;

        entity.UserGroups.Clear();
        foreach (var group in userGroups)
            entity.UserGroups.Add(group);

        if (entity.ArchivedDate.HasValue)
            entity.Status = VndStatus.Archived;

        // --- Реквизиты конкретной редакции (если она есть — у только что созданного ВНД без
        // единой загруженной редакции target-а нет, тогда применяем к документу как раньше,
        // для обратной совместимости с формой создания) ---
        var isCurrentRedaction = targetRedaction is not null && targetRedaction.Id == entity.CurrentRedactionId;

        void ApplyTo(VndDocument? doc, VndRedaction? redaction)
        {
            if (redaction is not null)
            {
                redaction.TitleRu = request.TitleRu;
                redaction.TitleEn = request.TitleEn;
                redaction.TitleKg = request.TitleKg;
                redaction.TypeId = request.TypeId;
                redaction.OrganId = request.OrganId;
                redaction.DeveloperId = developerId;
                redaction.CuratorDeveloperId = request.CuratorDeveloperId;
                redaction.AdoptionDate = request.AdoptionDate;
                redaction.AdoptionCode = request.AdoptionCode;
                redaction.EffectiveDate = request.EffectiveDate;
                redaction.SecrecyLevelId = request.SecrecyLevelId ?? redaction.SecrecyLevelId;
                redaction.ResponsibleExecutors.Clear();
                foreach (var executor in responsibleExecutors) redaction.ResponsibleExecutors.Add(executor);
                redaction.Keywords.Clear();
                foreach (var keyword in keywords) redaction.Keywords.Add(keyword);
                redaction.Rubrics.Clear();
                foreach (var rubric in rubrics) redaction.Rubrics.Add(rubric);
            }

            if (doc is not null)
            {
                doc.TitleRu = request.TitleRu;
                doc.TitleEn = request.TitleEn;
                doc.TitleKg = request.TitleKg;
                doc.TypeId = request.TypeId;
                doc.OrganId = request.OrganId;
                doc.DeveloperId = developerId;
                doc.CuratorDeveloperId = request.CuratorDeveloperId;
                doc.AdoptionDate = request.AdoptionDate;
                doc.AdoptionCode = request.AdoptionCode;
                doc.EffectiveDate = request.EffectiveDate;
                doc.SecrecyLevelId = request.SecrecyLevelId ?? doc.SecrecyLevelId;
                doc.ResponsibleExecutors.Clear();
                foreach (var executor in responsibleExecutors) doc.ResponsibleExecutors.Add(executor);
                doc.Keywords.Clear();
                foreach (var keyword in keywords) doc.Keywords.Add(keyword);
                doc.Rubrics.Clear();
                foreach (var rubric in rubrics) doc.Rubrics.Add(rubric);
            }
        }

        if (targetRedaction is not null)
        {
            // Doc.ResponsibleExecutors/Keywords/Rubrics не загружены в этом запросе (Include не
            // делали — они больше не источник правды) — если нужно зеркалировать в документ,
            // подгружаем их отдельно только в этом случае.
            VndDocument? docForMirror = null;
            if (isCurrentRedaction)
            {
                docForMirror = await _db.VndDocuments
                    .Include(x => x.ResponsibleExecutors)
                    .Include(x => x.Keywords)
                    .Include(x => x.Rubrics)
                    .FirstAsync(x => x.Id == id);
            }

            ApplyTo(docForMirror, targetRedaction);
        }
        else
        {
            var docForMirror = await _db.VndDocuments
                .Include(x => x.ResponsibleExecutors)
                .Include(x => x.Keywords)
                .Include(x => x.Rubrics)
                .FirstAsync(x => x.Id == id);
            ApplyTo(docForMirror, null);
        }

        // "Изменение реквизитов" проставляется автоматически, руками эту дату задать нельзя
        entity.RequisitesChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync();

        // Перечитываем документ с нужными Include-ами для ответа (заголовок/тип общие, остальное
        // могло измениться либо на нём самом, либо только на редакции).
        var reloaded = await _db.VndDocuments
                           .Include(x => x.Type)
                           .Include(x => x.Developer)
                           .Include(x => x.CuratorDeveloper)
                           .Include(x => x.Organ)
                           .Include(x => x.ResponsibleExecutors)
                           .Include(x => x.Rubrics)
                           .Include(x => x.Keywords)
                           .Include(x => x.UserGroups)
                           .Include(x => x.Redactions)
                           .Include(x => x.CreatedByUser)
                           .Include(x => x.ActualizationResponsibleUser)
                           .FirstOrDefaultAsync(x => x.Id == id)
                       ?? throw new KeyNotFoundException($"ВНД с id={id} не найден");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var canViewExtended = _currentUser.HasPermission(PermissionCode.ViewVndRegistryExtended);
        return ToResponse(reloaded, languageCode, today, canViewExtended);
    }

    public async Task<VndLinksResponse> GetLinksAsync(int vndId, string languageCode)
    {
        var exists = await _db.VndDocuments.AnyAsync(x => x.Id == vndId);
        if (!exists) throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var outgoing = await _db.Set<VndLink>()
            .Where(l => l.SourceVndId == vndId)
            .Include(l => l.TargetVnd)
            .ToListAsync();

        var incoming = await _db.Set<VndLink>()
            .Where(l => l.TargetVndId == vndId)
            .Include(l => l.SourceVnd)
            .ToListAsync();

        return new VndLinksResponse
        {
            Outgoing = outgoing.Select(l => ToLinkResponse(l.Id, l.TargetVnd!, languageCode)).ToList(),
            Incoming = incoming.Select(l => ToLinkResponse(l.Id, l.SourceVnd!, languageCode)).ToList()
        };
    }

    public async Task<VndLinkResponse> AddLinkAsync(int vndId, AddVndLinkRequest request, string languageCode)
    {
        if (vndId == request.TargetVndId)
            throw new InvalidOperationException("Документ не может ссылаться сам на себя");

        var sourceExists = await _db.VndDocuments.AnyAsync(x => x.Id == vndId);
        if (!sourceExists) throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var target = await _db.VndDocuments.FindAsync(request.TargetVndId)
                     ?? throw new KeyNotFoundException($"ВНД с id={request.TargetVndId} не найден");

        // Ограничение: привязать можно только действующий документ.
        // Дальнейшая судьба target (архив/отклонение) на саму связь не влияет
        if (target.Status != VndStatus.Active)
            throw new InvalidOperationException("Ссылку можно добавить только на действующий ВНД");

        var alreadyLinked = await _db.Set<VndLink>()
            .AnyAsync(l => l.SourceVndId == vndId && l.TargetVndId == request.TargetVndId);
        if (alreadyLinked)
            throw new InvalidOperationException("Ссылка на этот документ уже добавлена");

        var link = new VndLink { SourceVndId = vndId, TargetVndId = request.TargetVndId };
        _db.Set<VndLink>().Add(link);
        await _db.SaveChangesAsync();

        return ToLinkResponse(link.Id, target, languageCode);
    }

    public async Task DeleteLinkAsync(int vndId, int linkId)
    {
        var link = await _db.Set<VndLink>()
                       .FirstOrDefaultAsync(l => l.Id == linkId && (l.SourceVndId == vndId || l.TargetVndId == vndId))
                   ?? throw new KeyNotFoundException($"Связь с id={linkId} не найдена");

        _db.Set<VndLink>().Remove(link);
        await _db.SaveChangesAsync();
    }

    private static VndLinkResponse ToLinkResponse(int linkId, VndDocument doc, string languageCode) => new()
    {
        Id = linkId,
        VndId = doc.Id,
        Code = doc.Code,
        Title = doc.ResolveTitle(languageCode),
        Status = MapStatusBack(doc.Status)
    };

    public async Task<VndRedactionResponse> EditLastRevisionDirectlyAsync(
        int vndId, EditLastRevisionDirectlyRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var lastRedaction = await IncludeRequisites(_db.VndRedactions
                                .Where(r => r.VndId == vndId)
                                .Include(r => r.Attachments).ThenInclude(a => a.FileAttachment))
                            .OrderByDescending(r => r.Number)
                            .FirstOrDefaultAsync()
                            ?? throw new InvalidOperationException("У ВНД ещё нет ни одной редакции");

        // Редакция, отправленная на согласование, редактируется только через отзыв согласования
        // (VndApprovalService.CancelAsync возвращает её в черновик) — иначе главный редактор мог бы
        // незаметно подменить файл, который уже смотрят согласующие. Действующую редакцию (не
        // отправленную на согласование — ApprovalStatus NotRequired/Draft/Approved/Rejected) это
        // не ограничивает, её можно менять напрямую, как и раньше.
        if (lastRedaction.ApprovalStatus == RedactionApprovalStatus.Pending)
            throw new InvalidOperationException(
                "Редакция отправлена на согласование — сначала отзовите согласование во вкладке " +
                "«Согласование», чтобы редактировать её напрямую");

        var hasChanges = false;

        if (request.DocRu is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocRu, currentUserId);
            lastRedaction.DocFileRuId = saved.Id;
            hasChanges = true;
        }

        if (request.DocKg is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocKg, currentUserId);
            lastRedaction.DocFileKgId = saved.Id;
            hasChanges = true;
        }
        else if (request.RemoveDocKg && lastRedaction.DocFileKgId is not null)
        {
            // Явное удаление документа на кыргызском без замены (тот же паттерн, что и в
            // VndApprovalService.ResubmitAfterRevisionAsync).
            lastRedaction.DocFileKgId = null;
            hasChanges = true;
        }

        if (request.DocEn is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocEn, currentUserId);
            lastRedaction.DocFileEnId = saved.Id;
            hasChanges = true;
        }
        else if (request.RemoveDocEn && lastRedaction.DocFileEnId is not null)
        {
            lastRedaction.DocFileEnId = null;
            hasChanges = true;
        }

        if (request.Description is not null && request.Description != lastRedaction.Description)
        {
            lastRedaction.Description = request.Description;
            hasChanges = true;
        }

        // Новые вложения - добавляем в уже отслеживаемую EF навигацию (тот же паттерн, что и в
        // VndApprovalService.ResubmitAfterRevisionAsync).
        foreach (var file in request.NewAttachments ?? [])
        {
            var saved = await _fileService.SaveAsync(file, currentUserId);
            lastRedaction.Attachments.Add(new VndRedactionAttachment { FileAttachmentId = saved.Id });
            hasChanges = true;
        }

        if (request.RemovedAttachmentFileIds is { Count: > 0 })
        {
            var toRemove = lastRedaction.Attachments
                .Where(a => request.RemovedAttachmentFileIds.Contains(a.FileAttachmentId))
                .ToList();
            if (toRemove.Count > 0)
            {
                _db.Set<VndRedactionAttachment>().RemoveRange(toRemove);
                hasChanges = true;
            }
        }

        // RevisionChangedDate фиксирует факт правки содержимого редакции — обновляем,
        // только если реально что-то поменялось (не на пустой запрос).
        // DueActualizationDate, Period, ActualizationResponsibleUserId и статус ВНД
        // намеренно не трогаем — это прямое редактирование "как есть", без цикла актуализации.
        if (hasChanges)
            vnd.RevisionChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync();

        return ToRedactionResponse(lastRedaction, vnd);
    }

    /// <summary>Кнопка "Сформировать или загрузить ТИД" — прикладывает файл ТИД (Таблица изменений
    /// и дополнений) к уже загруженному черновику последней редакции отдельным шагом, после того
    /// как поле ТИД убрали из самой формы загрузки редакции (см. AddRedactionAsync выше). Пока
    /// редакция остаётся черновиком (не отправлена на согласование), ТИД можно приложить или
    /// заменить сколько угодно раз; отправить на согласование или опубликовать без согласования
    /// такую редакцию без ТИД нельзя — см. VndApprovalService.StartAsync и
    /// PublishRedactionWithoutApprovalAsync.</summary>
    public async Task<VndRedactionResponse> UploadTidForLastRedactionAsync(
        int vndId, UploadRedactionTidRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (!IsChiefEditor() && !await IsLinkedToVndAsync(vnd, currentUserId))
            throw new UnauthorizedAccessException(
                "Приложить ТИД может только разработчик, куратор, ответственный исполнитель, " +
                "инициатор, ответственный за актуализацию или главный редактор ВНД");

        var lastRedaction = await IncludeRequisites(_db.VndRedactions
                                .Where(r => r.VndId == vndId)
                                .Include(r => r.Attachments).ThenInclude(a => a.FileAttachment))
                            .OrderByDescending(r => r.Number)
                            .FirstOrDefaultAsync()
                            ?? throw new InvalidOperationException("У ВНД ещё нет ни одной редакции");

        if (lastRedaction.ApprovalStatus != RedactionApprovalStatus.Draft)
            throw new InvalidOperationException(
                "Приложить ТИД можно только к редакции в статусе черновика (ещё не отправленной на согласование)");

        var saved = await _fileService.SaveAsync(request.Tid, currentUserId);
        lastRedaction.TidFileId = saved.Id;

        // Если эта редакция изначально не требовала согласования (актуализация без
        // согласования), но не стала текущей сразу при загрузке именно из-за отсутствующего
        // ТИД (см. blockedByMissingTid в AddRedactionAsync) — теперь, когда ТИД приложен,
        // нужно довыполнить тот же переход, что случился бы сразу при загрузке, будь ТИД уже
        // на месте: сделать редакцию текущей и перевести ВНД в консолидацию/действующий.
        if (!lastRedaction.RequiresApproval && vnd.CurrentRedactionId != lastRedaction.Id)
        {
            lastRedaction.ApprovalStatus = RedactionApprovalStatus.NotRequired;
            vnd.CurrentRedactionId = lastRedaction.Id;
            vnd.RevisionChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);

            var enteringConsolidation = vnd.Status == VndStatus.OnActualization;
            vnd.Status = enteringConsolidation ? VndStatus.Consolidation : VndStatus.Active;

            if (enteringConsolidation)
                await StampConsolidationStartedAsync(vndId);
        }

        await _db.SaveChangesAsync();

        return ToRedactionResponse(lastRedaction, vnd);
    }

    public async Task<List<VndQuickSearchResponse>> QuickSearchAsync(string query, string languageCode, int limit)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        IQueryable<VndDocument> dbQuery = _db.VndDocuments.AsNoTracking();

        dbQuery = dbQuery.Where(x =>
            EF.Functions.ILike(x.Code, $"%{query}%") ||
            EF.Functions.ILike(x.TitleRu, $"%{query}%") ||
            (x.TitleEn != null && EF.Functions.ILike(x.TitleEn, $"%{query}%")) ||
            (x.TitleKg != null && EF.Functions.ILike(x.TitleKg, $"%{query}%")));

        dbQuery = ApplyDraftVisibilityFilter(dbQuery, null);

        var entities = await dbQuery
            .OrderByDescending(x => x.UpdatedAt)
            .Take(limit)
            .ToListAsync();

        return entities.Select(x => new VndQuickSearchResponse
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.ResolveTitle(languageCode),
            Status = MapStatusBack(x.Status)
        }).ToList();
    }
}