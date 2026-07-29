using delosfera_server.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public class KeywordService : IKeywordService
{
    private const int MaxDepth = 5;

    private readonly DelosferaDbContext _db;

    public KeywordService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<KeywordResponse>> GetAllAsync(KeywordSortBy sortBy, string? search, string languageCode)
    {
        IQueryable<Keyword> query = _db.Keywords;

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
            KeywordSortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            KeywordSortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            KeywordSortBy.NameAsc => query.OrderBy(x => x.TitleRu),
            KeywordSortBy.NameDesc => query.OrderByDescending(x => x.TitleRu),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }

    public async Task<KeywordResponse> CreateAsync(CreateKeywordRequest request, string languageCode)
    {
        if (request.ParentId.HasValue)
        {
            await EnsureParentExistsAsync(request.ParentId.Value);
            await EnsureDepthNotExceededAsync(request.ParentId.Value);
        }

        var entity = new Keyword
        {
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg,
            ParentId = request.ParentId
        };

        _db.Keywords.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<KeywordResponse> UpdateAsync(int id, UpdateKeywordRequest request, string languageCode)
    {
        var entity = await _db.Keywords.FindAsync(id)
            ?? throw new KeyNotFoundException($"Ключевое слово с id={id} не найдено");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new InvalidOperationException("Ключевое слово не может быть родителем самого себя");

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
        var entity = await _db.Keywords.FindAsync(id)
            ?? throw new KeyNotFoundException($"Ключевое слово с id={id} не найдено");

        var hasChildren = await _db.Keywords.AnyAsync(x => x.ParentId == id);
        if (hasChildren)
            throw new InvalidOperationException(
                "Нельзя удалить ключевое слово — у него есть дочерние записи. Сначала удалите или перенесите их");

        _db.Keywords.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить ключевое слово — на него есть ссылки в других документах");
        }
    }

    private async Task EnsureParentExistsAsync(int parentId)
    {
        var exists = await _db.Keywords.AnyAsync(x => x.Id == parentId);
        if (!exists)
            throw new KeyNotFoundException($"Родительское ключевое слово с id={parentId} не найдено");
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

            currentId = await _db.Keywords
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

            currentId = await _db.Keywords
                .Where(x => x.Id == currentId.Value)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync();
        }
    }

    private static KeywordResponse ToResponse(Keyword entity, string languageCode) => new()
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