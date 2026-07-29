using delosfera_server.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public class SecurityLevelService : ISecurityLevelService
{
    private readonly DelosferaDbContext _db;

    public SecurityLevelService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<SecurityLevelResponse>> GetAllAsync(SecurityLevelSortBy sortBy, string? search, string languageCode)
    {
        IQueryable<SecurityLevel> query = _db.SecurityLevels;

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
            SecurityLevelSortBy.CreatedAtAsc => query.OrderBy(t => t.CreatedAt),
            SecurityLevelSortBy.CreatedAtDesc => query.OrderByDescending(t => t.CreatedAt),
            SecurityLevelSortBy.NameAsc => query.OrderBy(t => t.TitleRu),
            SecurityLevelSortBy.NameDesc => query.OrderByDescending(t => t.TitleRu),
            _ => query.OrderBy(t => t.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(t => ToResponse(t, languageCode)).ToList();
    }

    public async Task<SecurityLevelResponse> CreateAsync(CreateSecurityLevelRequest request, string languageCode)
    {
        var entity = new SecurityLevel
        {
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg
        };

        _db.SecurityLevels.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<SecurityLevelResponse> UpdateAsync(int id, UpdateSecurityLevelRequest request, string languageCode)
    {
        var entity = await _db.SecurityLevels.FindAsync(id)
            ?? throw new KeyNotFoundException($"Уровень секретности с id={id} не найден");

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.SecurityLevels.FindAsync(id)
            ?? throw new KeyNotFoundException($"Уровень секретности с id={id} не найден");

        _db.SecurityLevels.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить уровень секретности — на него есть ссылки в других документах");
        }
    }

    private static SecurityLevelResponse ToResponse(SecurityLevel entity, string languageCode) => new()
    {
        Id = entity.Id,
        Name = entity.ResolveTitle(languageCode),
        TitleRu = entity.TitleRu,
        TitleEn = entity.TitleEn,
        TitleKg = entity.TitleKg,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}