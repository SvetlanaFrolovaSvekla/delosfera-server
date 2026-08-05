using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Models;
using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Vnd.Services;

public class VndService : IVndService
{
    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _fileService;
    private readonly ICurrentUserService _currentUser;

    public VndService(DelosferaDbContext db, IFileStorageService fileService, ICurrentUserService currentUser)
    {
        _db = db;
        _fileService = fileService;
        _currentUser = currentUser;
    }

    public async Task<List<VndResponse>> SearchAsync(VndSearchRequest request, string languageCode)
    {
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

        query = ApplyLinkedToMeFilter(query, request.LinkedToMeOnly);
        query = ApplyDraftVisibilityFilter(query, request.DraftOwnerScope);

        var entities = await query.ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return entities.Select(x => ToResponse(x, languageCode, today)).ToList();
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
        return ToResponse(entity, languageCode, today);
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

    /// <summary>"Только связанные со мной" — текущий пользователь является инициатором
    /// согласования, согласующим на одном из этапов, либо ответственным за текущий цикл
    /// актуализации (та же ответственность распространяется и на консолидацию — см.
    /// VndActualizationService.PublishAsync, где для консолидации без активного цикла
    /// актуализации проверяется именно инициатор согласования).</summary>
    private IQueryable<VndDocument> ApplyLinkedToMeFilter(IQueryable<VndDocument> query, bool linkedToMeOnly)
    {
        if (!linkedToMeOnly) return query;

        var userId = _currentUser.UserId;

        return query.Where(x =>
            x.ActualizationResponsibleUserId == userId ||
            _db.VndApprovalProcesses.Any(p => p.VndId == x.Id && p.InitiatorUserId == userId) ||
            _db.VndApprovalProcesses.Any(p => p.VndId == x.Id && p.Stages.Any(s => s.ApproverUserId == userId)));
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

    private static IQueryable<VndDocument> ApplyDateFilter(
        IQueryable<VndDocument> query, DateRangeFilter? filter,
        System.Linq.Expressions.Expression<Func<VndDocument, DateOnly?>> selector)
    {
        if (filter is null) return query;

        var compiled = selector.Compile();

        if (filter.Exact.HasValue)
            return query.Where(x => compiled(x) == filter.Exact.Value);

        if (filter.From.HasValue)
            query = query.Where(x => compiled(x) != null && compiled(x) >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(x => compiled(x) != null && compiled(x) <= filter.To.Value);

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

    private static VndResponse ToResponse(VndDocument x, string languageCode, DateOnly today) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.ResolveTitle(languageCode),
        TitleRu = x.TitleRu,
        TitleEn = x.TitleEn,
        TitleKg = x.TitleKg,
        Status = MapStatusBack(x.Status),
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
        UpdatedAt = x.UpdatedAt
    };

    private static VndRedactionResponse ToRedactionResponse(VndRedaction x, int? currentRedactionId) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Number = x.Number,
        Description = x.Description,
        IsCurrent = x.Id == currentRedactionId,
        DocFileRuId = x.DocFileRuId,
        DocFileKgId = x.DocFileKgId,
        DocFileEnId = x.DocFileEnId,
        RequiresApproval = x.RequiresApproval,
        ApprovalStatus = x.ApprovalStatus.ToString(),
        AttachmentFileIds = x.Attachments.Select(a => a.FileAttachmentId).ToList(),
        CreatedAt = x.CreatedAt
    };


    public async Task<VndResponse> CreateAsync(CreateVndRequest request, int currentUserId, string languageCode)
    {
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

        // currentUser уже отслеживается этим же DbContext (загружен выше),
        // поэтому EF автоматически восстановит навигацию entity.CreatedByUser (relationship fixup) —
        // отдельный Include/reload здесь не нужен.
        return ToResponse(entity, languageCode, today);
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
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        // Правило: последняя редакция не должна быть незавершённой (черновик или на согласовании)
        var lastRedaction = await _db.VndRedactions
            .Where(r => r.VndId == vndId)
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

        var docRu = await _fileService.SaveAsync(request.DocRu, currentUserId);
        var docKg = request.DocKg is not null ? await _fileService.SaveAsync(request.DocKg, currentUserId) : null;
        var docEn = request.DocEn is not null ? await _fileService.SaveAsync(request.DocEn, currentUserId) : null;

        var attachmentEntities = new List<VndRedactionAttachment>();
        foreach (var file in request.Attachments ?? [])
        {
            var saved = await _fileService.SaveAsync(file, currentUserId);
            attachmentEntities.Add(new VndRedactionAttachment { FileAttachmentId = saved.Id });
        }

        var nextNumber = (lastRedaction?.Number ?? 0) + 1;

        var redaction = new VndRedaction
        {
            VndId = vndId,
            Number = nextNumber,
            Code = $"{vnd.Code}-Р{nextNumber}",
            Description = request.Description,
            DocFileRuId = docRu.Id,
            DocFileKgId = docKg?.Id,
            DocFileEnId = docEn?.Id,
            RequiresApproval = request.RequiresApproval,
            ApprovalStatus = request.RequiresApproval
                ? RedactionApprovalStatus.Draft
                : RedactionApprovalStatus.NotRequired,
            Attachments = attachmentEntities
        };

        _db.VndRedactions.Add(redaction);
        await _db.SaveChangesAsync();

        if (!request.RequiresApproval)
        {
            vnd.CurrentRedactionId = redaction.Id;
            vnd.RevisionChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Если документ был в цикле актуализации - консолидация обязательна,
            // даже если конкретно эта редакция не требовала согласования.
            // Иначе (первая редакция нового ВНД, или обычное обновление активного
            // документа без согласования, без консолидации) - сразу становится действующим, как раньше.
            vnd.Status = vnd.Status == VndStatus.OnActualization
                ? VndStatus.Consolidation
                : VndStatus.Active;

            await _db.SaveChangesAsync();
        }

        return ToRedactionResponse(redaction, vnd.CurrentRedactionId);
    }

    // Отправка на согласование (заглушка)
    public async Task<VndRedactionResponse> SubmitRedactionForApprovalAsync(int vndId, int redactionId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var redaction = await _db.VndRedactions
                            .Include(x => x.Attachments)
                            .FirstOrDefaultAsync(x => x.Id == redactionId && x.VndId == vndId)
                        ?? throw new KeyNotFoundException($"Редакция с id={redactionId} не найдена");

        if (redaction.ApprovalStatus != RedactionApprovalStatus.Draft)
            throw new InvalidOperationException("Отправить на согласование можно только черновик редакции");

        // если RequiresApproval = true, то черновик редакции ещё
        // не отправлен на согласование, остается черновиком
        redaction.ApprovalStatus = RedactionApprovalStatus.Pending;
        vnd.Status = VndStatus.Review;
        await _db.SaveChangesAsync();

        return ToRedactionResponse(redaction, vnd.CurrentRedactionId);
    }


    public async Task<List<VndRedactionResponse>> GetRedactionsAsync(int vndId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var redactions = await _db.VndRedactions
            .Where(x => x.VndId == vndId)
            .Include(x => x.Attachments)
            .OrderBy(x => x.Number)
            .ToListAsync();

        return redactions.Select(r => ToRedactionResponse(r, vnd.CurrentRedactionId)).ToList();
    }

    public async Task<VndResponse> UpdateRequisitesAsync(int id, UpdateVndRequisitesRequest request,
        string languageCode)
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

        var typeExists = await _db.TypesVnd.AnyAsync(x => x.Id == request.TypeId);
        if (!typeExists) throw new KeyNotFoundException($"Вид ВНД с id={request.TypeId} не найден");

        var organExists = await _db.ApprovalBodies.AnyAsync(x => x.Id == request.OrganId);
        if (!organExists) throw new KeyNotFoundException($"Орган утверждения с id={request.OrganId} не найден");

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
            developerId = entity.DeveloperId; // не меняем, если не передали
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

        // --- Применяем изменения ---
        entity.TypeId = request.TypeId;
        entity.OrganId = request.OrganId;
        entity.DeveloperId = developerId;
        entity.CuratorDeveloperId = request.CuratorDeveloperId;

        entity.ResponsibleExecutors.Clear();
        foreach (var executor in responsibleExecutors)
            entity.ResponsibleExecutors.Add(executor);

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;

        entity.AdoptionDate = request.AdoptionDate;
        entity.AdoptionCode = request.AdoptionCode;
        entity.EffectiveDate = request.EffectiveDate;

        entity.DueActualizationDate = request.DueActualizationDate;
        entity.LastActualizationDate = request.LastActualizationDate;
        entity.LastActualizationHadChanges = request.LastActualizationHadChanges;

        entity.CancelDate = request.CancelDate;
        entity.CancelCode = request.CancelCode;
        entity.CancelReason = request.CancelReason;
        entity.ArchivedDate = request.ArchivedDate;

        entity.Keywords.Clear();
        foreach (var keyword in keywords)
            entity.Keywords.Add(keyword);

        entity.Rubrics.Clear();
        foreach (var rubric in rubrics)
            entity.Rubrics.Add(rubric);

        entity.UserGroups.Clear();
        foreach (var group in userGroups)
            entity.UserGroups.Add(group);

        entity.SecrecyLevelId = request.SecrecyLevelId ?? entity.SecrecyLevelId;

        if (entity.ArchivedDate.HasValue)
            entity.Status = VndStatus.Archived;
        else if (entity.CancelDate.HasValue && entity.Status != VndStatus.Draft)
            entity.Status = entity.Status;

        // "Изменение реквизитов" проставляется автоматически, руками эту дату задать нельзя
        entity.RequisitesChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return ToResponse(entity, languageCode, today);
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

        var lastRedaction = await _db.VndRedactions
                                .Where(r => r.VndId == vndId)
                                .OrderByDescending(r => r.Number)
                                .Include(r => r.Attachments)
                                .FirstOrDefaultAsync()
                            ?? throw new InvalidOperationException("У ВНД ещё нет ни одной редакции");

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

        if (request.DocEn is not null)
        {
            var saved = await _fileService.SaveAsync(request.DocEn, currentUserId);
            lastRedaction.DocFileEnId = saved.Id;
            hasChanges = true;
        }

        if (request.Description is not null && request.Description != lastRedaction.Description)
        {
            lastRedaction.Description = request.Description;
            hasChanges = true;
        }

        // RevisionChangedDate фиксирует факт правки содержимого редакции — обновляем,
        // только если реально что-то поменялось (не на пустой запрос).
        // DueActualizationDate, Period, ActualizationResponsibleUserId и статус ВНД
        // намеренно не трогаем — это прямое редактирование "как есть", без цикла актуализации.
        if (hasChanges)
            vnd.RevisionChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync();

        return ToRedactionResponse(lastRedaction, vnd.CurrentRedactionId);
    }
}