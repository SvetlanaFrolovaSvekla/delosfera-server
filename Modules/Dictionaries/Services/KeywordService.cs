using delosfera_server.Common.Extensions;
using delosfera_server.Common.Services;
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
            await HierarchyValidation.EnsureParentExistsAsync(_db.Keywords, request.ParentId.Value,
                pid => $"Родительское ключевое слово с id={pid} не найдено");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.Keywords, request.ParentId.Value, MaxDepth,
                depth => $"Превышена максимальная глубина вложенности ({depth} уровней)");
        }

        var entity = new Keyword
        {
            TitleRu = request.TitleRu, TitleEn = request.TitleEn, TitleKg = request.TitleKg,
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

            await HierarchyValidation.EnsureParentExistsAsync(_db.Keywords, request.ParentId.Value,
                pid => $"Родительское ключевое слово с id={pid} не найдено");
            await HierarchyValidation.EnsureNoCircularReferenceAsync(_db.Keywords, id, request.ParentId.Value,
                "Нельзя выбрать родителем один из дочерних элементов — это создаст циклическую ссылку");
            await HierarchyValidation.EnsureDepthNotExceededAsync(_db.Keywords, request.ParentId.Value, MaxDepth,
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