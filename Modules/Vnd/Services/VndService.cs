using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Models;
using delosfera_server.Common.Extensions;
using delosfera_server.Modules.Files.Services;

namespace delosfera_server.Modules.Vnd.Services;

public class VndService : IVndService
{
    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _fileService;

    public VndService(DelosferaDbContext db, IFileStorageService fileService)
    {
        _db = db;
        _fileService = fileService;
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
            .Include(x => x.Redactions);

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

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
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
                         .FirstOrDefaultAsync(x => x.Id == id)
                     ?? throw new KeyNotFoundException($"ВНД с id={id} не найден");

        return ToResponse(entity, languageCode);
    }

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

    private static VndResponse ToResponse(VndDocument x, string languageCode) => new()
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
        KeywordIds = x.Keywords.Select(k => k.Id).ToList(),
        RubricIds = x.Rubrics.Select(r => r.Id).ToList(),
        SecrecyLevelId = x.SecrecyLevelId,
        UserGroupIds = x.UserGroups.Select(g => g.Id).ToList(),
        RedactionIds = x.Redactions.Select(r => r.Id).ToList(),
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };

    private static VndRedactionResponse ToRedactionResponse(VndRedaction x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Number = x.Number,
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
            LastActualizationDate = today,
            DueActualizationDate = dueDate,
            LastActualizationHadChanges = false
        };

        _db.VndDocuments.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
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

    public async Task<VndRedactionResponse> AddRedactionAsync(
        int vndId, CreateVndRedactionRequest request, int currentUserId)
    {
        var vnd = await _db.VndDocuments.FindAsync(vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        var docRu = await _fileService.SaveAsync(request.DocRu, currentUserId);
        var docKg = request.DocKg is not null ? await _fileService.SaveAsync(request.DocKg, currentUserId) : null;
        var docEn = request.DocEn is not null ? await _fileService.SaveAsync(request.DocEn, currentUserId) : null;

        var attachmentEntities = new List<VndRedactionAttachment>();
        foreach (var file in request.Attachments ?? [])
        {
            var saved = await _fileService.SaveAsync(file, currentUserId);
            attachmentEntities.Add(new VndRedactionAttachment { FileAttachmentId = saved.Id });
        }

        var nextNumber = (await _db.VndRedactions
            .Where(r => r.VndId == vndId)
            .Select(r => (int?)r.Number)
            .MaxAsync() ?? 0) + 1;

        var redaction = new VndRedaction
        {
            VndId = vndId,
            Number = nextNumber,
            Code = $"{vnd.Code}-Р{nextNumber}",
            DocFileRuId = docRu.Id,
            DocFileKgId = docKg?.Id,
            DocFileEnId = docEn?.Id,
            RequiresApproval = request.RequiresApproval,
            ApprovalStatus = request.RequiresApproval
                ? RedactionApprovalStatus.Pending
                : RedactionApprovalStatus.NotRequired,
            Attachments = attachmentEntities
        };

        _db.VndRedactions.Add(redaction);
        await _db.SaveChangesAsync(); // нужен Id редакции перед проставлением ссылки

        vnd.CurrentRedactionId = redaction.Id;
        vnd.RevisionChangedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync();

        return ToRedactionResponse(redaction);
    }
}