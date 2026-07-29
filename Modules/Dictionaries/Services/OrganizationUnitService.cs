using delosfera_server.Common.Extensions;
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

    public async Task<List<OrganizationUnitResponse>> GetAllAsync(OrganizationUnitSortBy sortBy, string? search, string languageCode)
    {
        IQueryable<OrganizationUnit> query = _db.OrganizationUnits;

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
            await EnsureParentExistsAsync(request.ParentId.Value);
            await EnsureDepthNotExceededAsync(request.ParentId.Value);
        }

        var entity = new OrganizationUnit
        {
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg,
            ParentId = request.ParentId
        };

        _db.OrganizationUnits.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<OrganizationUnitResponse> UpdateAsync(int id, UpdateOrganizationUnitRequest request, string languageCode)
    {
        var entity = await _db.OrganizationUnits.FindAsync(id)
            ?? throw new KeyNotFoundException($"Структурное подразделение с id={id} не найдено");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new InvalidOperationException("Подразделение не может быть родителем самого себя");

            await EnsureParentExistsAsync(request.ParentId.Value);
            await EnsureNoCircularReferenceAsync(id, request.ParentId.Value);
            await EnsureDepthNotExceededAsync(request.ParentId.Value);
        }

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        entity.ParentId = request.ParentId;
        await _db.SaveChangesAsync();

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

    private async Task EnsureParentExistsAsync(int parentId)
    {
        var exists = await _db.OrganizationUnits.AnyAsync(x => x.Id == parentId);
        if (!exists)
            throw new KeyNotFoundException($"Родительское подразделение с id={parentId} не найдено");
    }

    private async Task EnsureNoCircularReferenceAsync(int nodeId, int newParentId)
    {
        var currentId = (int?)newParentId;
        var visited = new HashSet<int>();

        while (currentId.HasValue)
        {
            if (currentId.Value == nodeId)
                throw new InvalidOperationException(
                    "Нельзя выбрать родителем один из дочерних элементов — это создаст циклическую ссылку");

            if (!visited.Add(currentId.Value))
                break;

            currentId = await _db.OrganizationUnits
                .Where(x => x.Id == currentId.Value)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync();
        }
    }

    private async Task EnsureDepthNotExceededAsync(int parentId)
    {
        var depth = 1;
        var currentId = (int?)parentId;

        while (currentId.HasValue)
        {
            depth++;
            if (depth > MaxDepth)
                throw new InvalidOperationException(
                    $"Превышена максимальная глубина вложенности ({MaxDepth} уровней)");

            currentId = await _db.OrganizationUnits
                .Where(x => x.Id == currentId.Value)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync();
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
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}