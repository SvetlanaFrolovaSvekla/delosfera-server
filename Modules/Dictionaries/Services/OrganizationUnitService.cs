using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public class OrganizationUnitService : IOrganizationUnitService
{
    private const int MaxDepth = 5;

    private readonly DelosferaDbContext _db;

    public OrganizationUnitService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<OrganizationUnitResponse>> GetAllAsync(OrganizationUnitSortBy sortBy, string? search,
        string languageCode)
    {
        IQueryable<OrganizationUnit> query = _db.OrganizationUnits
            .Include(x => x.HeadUser)
            .Include(x => x.CuratorUser);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.TitleRu, $"%{term}%") ||
                (x.TitleEn != null && EF.Functions.ILike(x.TitleEn, $"%{term}%")) ||
                (x.TitleKg != null && EF.Functions.ILike(x.TitleKg, $"%{term}%")));
        }

        query = sortBy switch
        {
            OrganizationUnitSortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            OrganizationUnitSortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            OrganizationUnitSortBy.NameAsc => query.OrderBy(x => x.TitleRu),
            OrganizationUnitSortBy.NameDesc => query.OrderByDescending(x => x.TitleRu),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }

    public async Task<OrganizationUnitResponse> CreateAsync(CreateOrganizationUnitRequest request, string languageCode)
    {
        if (request.ParentId.HasValue)
        {
            await HierarchyValidation.EnsureParentExistsAsync(_db.OrganizationUnits, request.ParentId.Value,
                pid => $"Родительское подразделение с id={pid} не найдено");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.OrganizationUnits, request.ParentId.Value,
                MaxDepth,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
        }

        if (request.HeadUserId.HasValue)
            await EnsureUserExistsAsync(request.HeadUserId.Value, "Начальник");
        if (request.CuratorUserId.HasValue)
            await EnsureUserExistsAsync(request.CuratorUserId.Value, "Куратор");

        var entity = new OrganizationUnit
        {
            TitleRu = request.TitleRu, TitleEn = request.TitleEn, TitleKg = request.TitleKg,
            ParentId = request.ParentId, HeadUserId = request.HeadUserId, CuratorUserId = request.CuratorUserId
        };

        _db.OrganizationUnits.Add(entity);
        await _db.SaveChangesAsync();

        return await LoadResponseAsync(entity.Id, languageCode);
    }

    public async Task<OrganizationUnitResponse> UpdateAsync(int id, UpdateOrganizationUnitRequest request,
        string languageCode)
    {
        var entity = await _db.OrganizationUnits.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Структурное подразделение с id={id} не найдено");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new InvalidOperationException("Подразделение не может быть родителем самого себя");

            await HierarchyValidation.EnsureParentExistsAsync(_db.OrganizationUnits, request.ParentId.Value,
                pid => $"Родительское подразделение с id={pid} не найдено");
            await HierarchyValidation.EnsureNoCircularReferenceAsync(_db.OrganizationUnits, id, request.ParentId.Value,
                "Нельзя выбрать родителем один из дочерних элементов — это создаст циклическую ссылку");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.OrganizationUnits, request.ParentId.Value,
                MaxDepth,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
        }

        if (request.HeadUserId.HasValue)
            await EnsureUserExistsAsync(request.HeadUserId.Value, "Начальник");
        if (request.CuratorUserId.HasValue)
            await EnsureUserExistsAsync(request.CuratorUserId.Value, "Куратор");

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        entity.ParentId = request.ParentId;
        entity.HeadUserId = request.HeadUserId;
        entity.CuratorUserId = request.CuratorUserId;
        await _db.SaveChangesAsync();

        return await LoadResponseAsync(id, languageCode);
    }

    private async Task EnsureUserExistsAsync(int userId, string role)
    {
        var exists = await _db.Users.AnyAsync(x => x.Id == userId);
        if (!exists) throw new KeyNotFoundException($"{role} с id={userId} не найден среди пользователей");
    }

    private async Task<OrganizationUnitResponse> LoadResponseAsync(int id, string languageCode)
    {
        var entity = await _db.OrganizationUnits
            .Include(x => x.HeadUser)
            .Include(x => x.CuratorUser)
            .FirstAsync(x => x.Id == id);
        return ToResponse(entity, languageCode);
    }


    public async Task DeleteAsync(int id)
    {
        var entity = await _db.OrganizationUnits.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Структурное подразделение с id={id} не найдено");

        var hasChildren = await _db.OrganizationUnits.AnyAsync(x => x.ParentId == id);
        if (hasChildren)
            throw new InvalidOperationException(
                "Нельзя удалить подразделение — у него есть дочерние записи. Сначала удалите или перенесите их");

        _db.OrganizationUnits.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить подразделение — на него есть ссылки в других документах");
        }
    }

    private static OrganizationUnitResponse ToResponse(OrganizationUnit entity, string languageCode) => new()
    {
        Id = entity.Id,
        Name = entity.ResolveTitle(languageCode),
        TitleRu = entity.TitleRu,
        TitleEn = entity.TitleEn,
        TitleKg = entity.TitleKg,
        ParentId = entity.ParentId,
        HeadUserId = entity.HeadUserId,
        HeadUserName = entity.HeadUser?.FullName,
        CuratorUserId = entity.CuratorUserId,
        CuratorUserName = entity.CuratorUser?.FullName,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}