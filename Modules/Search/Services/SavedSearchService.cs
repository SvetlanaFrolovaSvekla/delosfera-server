using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Search.DTO;
using delosfera_server.Modules.Search.Models;

namespace delosfera_server.Modules.Search.Services;

public interface ISavedSearchService
{
    Task<List<SavedSearchDto>> ListAsync(int userId);
    Task<SavedSearchDto> SaveAsync(SaveSearchRequest request, int userId);
    Task DeleteAsync(int id, int userId);
}

/// <summary>
/// Сохранённые фильтры поиска (GEN-04).
///
/// Фильтр принадлежит сотруднику: у делопроизводителя и у куратора закупок разные
/// рабочие выборки, и общий список превратился бы в свалку чужих запросов.
/// </summary>
public class SavedSearchService : ISavedSearchService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DelosferaDbContext _db;

    public SavedSearchService(DelosferaDbContext db) => _db = db;

    public async Task<List<SavedSearchDto>> ListAsync(int userId)
    {
        var rows = await _db.SavedSearches
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync();

        return rows.Select(ToDto).ToList();
    }

    public async Task<SavedSearchDto> SaveAsync(SaveSearchRequest request, int userId)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Укажите название фильтра");

        var criteria = JsonSerializer.Serialize(request.Criteria, Json);

        // Повторное сохранение под тем же названием — это правка фильтра, а не ошибка:
        // сотрудник уточняет свою рабочую выборку и ждёт, что она обновится.
        var existing = await _db.SavedSearches
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Name == name);

        if (existing is not null)
        {
            existing.Criteria = criteria;
            await _db.SaveChangesAsync();
            return ToDto(existing);
        }

        var saved = new SavedSearch {UserId = userId, Name = name, Criteria = criteria};

        _db.SavedSearches.Add(saved);
        await _db.SaveChangesAsync();

        return ToDto(saved);
    }

    public async Task DeleteAsync(int id, int userId)
    {
        var saved = await _db.SavedSearches.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId)
            ?? throw new KeyNotFoundException("Сохранённый фильтр не найден");

        _db.SavedSearches.Remove(saved);
        await _db.SaveChangesAsync();
    }

    private static SavedSearchDto ToDto(SavedSearch saved) => new()
    {
        Id = saved.Id,
        Name = saved.Name,
        CreatedAt = saved.CreatedAt,
        Criteria = Deserialize(saved.Criteria),
    };

    /// <summary>
    /// Условия старого фильтра могли быть сохранены прежней версией набора полей.
    /// Разбор не должен ронять список: непонятный фильтр открывается пустым, а не
    /// ломает всю страницу поиска.
    /// </summary>
    private static SearchRequest Deserialize(string criteria)
    {
        try
        {
            return JsonSerializer.Deserialize<SearchRequest>(criteria, Json) ?? new SearchRequest();
        }
        catch (JsonException)
        {
            return new SearchRequest();
        }
    }
}
