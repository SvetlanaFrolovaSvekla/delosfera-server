using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public class RubricService : IRubricService
{
    private const int MaxDepth = 5;

    private readonly DelosferaDbContext _db;

    public RubricService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<RubricResponse>> GetAllAsync(RubricSortBy sortBy, string? search, string languageCode)
    {
        IQueryable<Rubric> query = _db.Rubrics;

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
            RubricSortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            RubricSortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            RubricSortBy.NameAsc => query.OrderBy(x => x.TitleRu),
            RubricSortBy.NameDesc => query.OrderByDescending(x => x.TitleRu),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }

    public async Task<RubricResponse> CreateAsync(CreateRubricRequest request, string languageCode)
    {
        if (request.ParentId.HasValue)
        {
            await HierarchyValidation.EnsureParentExistsAsync(_db.Rubrics, request.ParentId.Value,
                pid => $"Родительская рубрика с id={pid} не найдена");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.Rubrics, request.ParentId.Value, MaxDepth,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
        }

        var entity = new Rubric
        {
            TitleRu = request.TitleRu, TitleEn = request.TitleEn, TitleKg = request.TitleKg,
            ParentId = request.ParentId
        };

        _db.Rubrics.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<RubricResponse> UpdateAsync(int id, UpdateRubricRequest request, string languageCode)
    {
        var entity = await _db.Rubrics.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Рубрика с id={id} не найдена");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new InvalidOperationException("Рубрика не может быть родителем самой себя");

            await HierarchyValidation.EnsureParentExistsAsync(_db.Rubrics, request.ParentId.Value,
                pid => $"Родительская рубрика с id={pid} не найдена");
            await HierarchyValidation.EnsureNoCircularReferenceAsync(_db.Rubrics, id, request.ParentId.Value,
                "Нельзя выбрать родителем один из дочерних элементов — это создаст циклическую ссылку");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.Rubrics, request.ParentId.Value, MaxDepth,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
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
        var entity = await _db.Rubrics.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Рубрика с id={id} не найдена");

        var hasChildren = await _db.Rubrics.AnyAsync(x => x.ParentId == id);
        if (hasChildren)
            throw new InvalidOperationException(
                "Нельзя удалить рубрику — у неё есть дочерние записи. Сначала удалите или перенесите их");

        _db.Rubrics.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить рубрику — на неё есть ссылки в других документах");
        }
    }

    private static RubricResponse ToResponse(Rubric entity, string languageCode) => new()
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