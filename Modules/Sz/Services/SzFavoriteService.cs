using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Sz.Models;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>Личное «Избранное» пользователя в реестре СЗ.</summary>
public interface ISzFavoriteService
{
    Task AddAsync(int szId, int userId);
    Task RemoveAsync(int szId, int userId);

    /// <summary>Добавить пачку записок (идемпотентно). Видимость проверяет вызывающий.</summary>
    Task AddManyAsync(IReadOnlyCollection<int> szIds, int userId);

    /// <summary>Убрать пачку записок из избранного (идемпотентно).</summary>
    Task RemoveManyAsync(IReadOnlyCollection<int> szIds, int userId);
}

public class SzFavoriteService : ISzFavoriteService
{
    private readonly DelosferaDbContext _db;

    public SzFavoriteService(DelosferaDbContext db)
    {
        _db = db;
    }

    /// <summary>Идемпотентно: повторное добавление ничего не меняет.</summary>
    public async Task AddAsync(int szId, int userId)
    {
        if (await _db.SzFavorites.AnyAsync(x => x.UserId == userId && x.SzDocumentId == szId)) return;

        _db.SzFavorites.Add(new SzFavorite { UserId = userId, SzDocumentId = szId, CreatedAt = DateTime.UtcNow });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Двойной клик / две вкладки: запись уже добавлена параллельным запросом — это и нужно.
        }
    }

    public async Task AddManyAsync(IReadOnlyCollection<int> szIds, int userId)
    {
        if (szIds.Count == 0) return;

        // Уже отмеченные пропускаем: первичный ключ (user_id, sz_document_id) не даст дубль.
        var existing = await _db.SzFavorites
            .Where(x => x.UserId == userId && szIds.Contains(x.SzDocumentId))
            .Select(x => x.SzDocumentId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var id in szIds.Except(existing))
            _db.SzFavorites.Add(new SzFavorite { UserId = userId, SzDocumentId = id, CreatedAt = now });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Гонка с параллельным запросом (двойной клик): часть строк уже добавлена.
            // Повторяем по одной — каждая вставка идемпотентна.
            foreach (var id in szIds)
            {
                _db.ChangeTracker.Clear();
                await AddAsync(id, userId);
            }
        }
    }

    public Task RemoveManyAsync(IReadOnlyCollection<int> szIds, int userId) =>
        _db.SzFavorites
            .Where(x => x.UserId == userId && szIds.Contains(x.SzDocumentId))
            .ExecuteDeleteAsync();

    /// <summary>Идемпотентно: удаление отсутствующей отметки — не ошибка.</summary>
    public Task RemoveAsync(int szId, int userId) =>
        _db.SzFavorites
            .Where(x => x.UserId == userId && x.SzDocumentId == szId)
            .ExecuteDeleteAsync();
}
