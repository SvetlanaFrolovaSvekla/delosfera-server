using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Личное "Избранное" текущего пользователя в реестре ВНД.</summary>
public interface IVndFavoriteService
{
    Task AddAsync(int vndId);
    Task RemoveAsync(int vndId);
}

public class VndFavoriteService : IVndFavoriteService
{
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VndFavoriteService(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Идемпотентно: повторное добавление ничего не меняет.</summary>
    public async Task AddAsync(int vndId)
    {
        var userId = _currentUser.UserId;
        if (!await _db.VndDocuments.AnyAsync(x => x.Id == vndId))
            throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (await _db.VndFavorites.AnyAsync(x => x.UserId == userId && x.VndId == vndId)) return;

        _db.VndFavorites.Add(new VndFavorite { UserId = userId, VndId = vndId, CreatedAt = DateTime.UtcNow });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Двойной клик / две вкладки: запись уже добавлена параллельным запросом — это и нужно.
        }
    }

    /// <summary>Идемпотентно: удаление отсутствующей отметки — не ошибка.</summary>
    public Task RemoveAsync(int vndId)
    {
        var userId = _currentUser.UserId;
        return _db.VndFavorites
            .Where(x => x.UserId == userId && x.VndId == vndId)
            .ExecuteDeleteAsync();
    }
}
