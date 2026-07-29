using delosfera_server.Common.Extensions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Dictionaries.Services;

public class PositionService : IPositionService
{
    private readonly DelosferaDbContext _db;

    public PositionService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<PositionResponse>> GetAllAsync(PositionSortBy sortBy, string? search, string languageCode)
    {
        IQueryable<Position> query = _db.Positions;

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
            PositionSortBy.CreatedAtAsc => query.OrderBy(x => x.CreatedAt),
            PositionSortBy.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt),
            PositionSortBy.NameAsc => query.OrderBy(x => x.TitleRu),
            PositionSortBy.NameDesc => query.OrderByDescending(x => x.TitleRu),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        var entities = await query.ToListAsync();
        return entities.Select(x => ToResponse(x, languageCode)).ToList();
    }

    public async Task<PositionResponse> CreateAsync(CreatePositionRequest request, string languageCode)
    {
        var entity = new Position
        {
            TitleRu = request.TitleRu,
            TitleEn = request.TitleEn,
            TitleKg = request.TitleKg
        };

        _db.Positions.Add(entity);
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task<PositionResponse> UpdateAsync(int id, UpdatePositionRequest request, string languageCode)
    {
        var entity = await _db.Positions.FindAsync(id)
            ?? throw new KeyNotFoundException($"Должность с id={id} не найдена");

        entity.TitleRu = request.TitleRu;
        entity.TitleEn = request.TitleEn;
        entity.TitleKg = request.TitleKg;
        await _db.SaveChangesAsync();

        return ToResponse(entity, languageCode);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Positions.FindAsync(id)
            ?? throw new KeyNotFoundException($"Должность с id={id} не найдена");

        _db.Positions.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Нельзя удалить должность — на неё есть ссылки в других документах");
        }
    }

    private static PositionResponse ToResponse(Position entity, string languageCode) => new()
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