using delosfera_server.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public class TypeVndService : ITypeVndService
{
    private readonly DelosferaDbContext _db;

    public TypeVndService(DelosferaDbContext db) => _db = db;

    public async Task<List<TypeVndResponse>> GetAllAsync(TypeVndSortBy sortBy, string? search, string languageCode)
    {
        IQueryable<TypeVnd> query = _db.TypesVnd;

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
            TypeVndSortBy.CreatedAtAsc => query.OrderBy(t => t.CreatedAt),
            TypeVndSortBy.CreatedAtDesc => query.OrderByDescending(t => t.CreatedAt),
            TypeVndSortBy.NameAsc => query.OrderBy(t => t.TitleRu),
            TypeVndSortBy.NameDesc => query.OrderByDescending(t => t.TitleRu),
            _ => query.OrderBy(t => t.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(t => ToResponse(t, languageCode)).ToList();
    }

    public async Task<TypeVndResponse> CreateAsync(CreateTypeVndRequest request, string languageCode)
    {
        var entity = new TypeVnd
        {
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg
        };

        _db.TypesVnd.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<TypeVndResponse> UpdateAsync(int id, UpdateTypeVndRequest request, string languageCode)
    {
        var entity = await _db.TypesVnd.FindAsync(id)
            ?? throw new KeyNotFoundException($"Тип ВНД с id={id} не найден");

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.TypesVnd.FindAsync(id)
                     ?? throw new KeyNotFoundException($"Тип ВНД с id={id} не найден");

        _db.TypesVnd.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Сработает, когда на TypeVnd появятся внешние ключи из модуля Vnd
            // (Postgres не даст удалить строку, на которую есть ссылки)
            throw new InvalidOperationException(
                "Нельзя удалить тип ВНД — на него есть ссылки в других документах");
        }
    }

    private static TypeVndResponse ToResponse(TypeVnd entity, string languageCode) => new()
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