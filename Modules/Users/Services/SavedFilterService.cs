using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

public interface ISavedFilterService
{
    Task<List<SavedFilterResponse>> ListAsync(int ownerUserId, string? scope);
    Task<SavedFilterResponse> CreateAsync(SavedFilterSaveRequest request, int ownerUserId);
    Task DeleteAsync(int id, int ownerUserId);
}

/// <summary>
/// Личные сохранённые фильтры реестров (БП-16). Условия хранятся строкой JSON; сервис
/// раскладывает их туда и обратно и следит, чтобы пользователь видел и правил только
/// свои фильтры.
/// </summary>
public class SavedFilterService : ISavedFilterService
{
    private readonly DelosferaDbContext _db;

    public SavedFilterService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<List<SavedFilterResponse>> ListAsync(int ownerUserId, string? scope)
    {
        var q = _db.SavedFilters.Where(f => f.OwnerUserId == ownerUserId);
        if (!string.IsNullOrWhiteSpace(scope))
            q = q.Where(f => f.Scope == scope);

        var rows = await q.OrderBy(f => f.Name).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<SavedFilterResponse> CreateAsync(SavedFilterSaveRequest request, int ownerUserId)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Укажите название фильтра");
        if (string.IsNullOrWhiteSpace(request.Scope))
            throw new InvalidOperationException("Не указана область фильтра");

        var now = DateTime.UtcNow;
        var entity = new SavedFilter
        {
            OwnerUserId = ownerUserId,
            Scope = request.Scope.Trim(),
            Name = name,
            Payload = request.Payload.GetRawText(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.SavedFilters.Add(entity);
        await _db.SaveChangesAsync();
        return Map(entity);
    }

    public async Task DeleteAsync(int id, int ownerUserId)
    {
        var entity = await _db.SavedFilters.FirstOrDefaultAsync(f => f.Id == id && f.OwnerUserId == ownerUserId)
            ?? throw new KeyNotFoundException("Фильтр не найден");

        _db.SavedFilters.Remove(entity);
        await _db.SaveChangesAsync();
    }

    private static SavedFilterResponse Map(SavedFilter f) => new()
    {
        Id = f.Id,
        Scope = f.Scope,
        Name = f.Name,
        Payload = ParsePayload(f.Payload),
        UpdatedAt = f.UpdatedAt,
    };

    // Битый JSON старого фильтра не должен рушить список — вернём пустой объект,
    // фильтр останется применимым хотя бы по названию.
    private static JsonElement ParsePayload(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }
    }
}
