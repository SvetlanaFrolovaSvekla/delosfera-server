using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Services;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>
/// Звёздочка «Избранное» у служебной записки. Список избранного — вкладка «Избранное»
/// в реестре (POST api/sz/search с FavoritesOnly = true), признак — SzListItem.IsFavorite.
/// </summary>
[ApiController]
[Route("api/sz/{id:int}/favorite")]
[Tags("СЗ — Избранное")]
[Authorize]
public class SzFavoritesController : ControllerBase
{
    private readonly ISzFavoriteService _favorites;
    private readonly ISzService _sz;
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SzFavoritesController(
        ISzFavoriteService favorites, ISzService sz, DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _favorites = favorites;
        _sz = sz;
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>Добавить записку в избранное текущего пользователя.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(int id)
    {
        var userId = _currentUser.UserId;

        // В избранное — только то, что пользователю видно в реестре: иначе перебором id
        // можно было бы узнать, какие записки существуют.
        if (!await _db.SzDocuments.AnyAsync(x => x.Id == id))
            return NotFound(new { message = "Служебная записка не найдена" });
        if (!await _sz.CanViewAsync(id, userId))
            return StatusCode(403, new { message = "Нет доступа к этой служебной записке" });

        await _favorites.AddAsync(id, userId);
        return NoContent();
    }

    /// <summary>Сколько записок можно отметить за раз — с запасом на страницу реестра (100 строк).</summary>
    private const int BulkLimit = 500;

    /// <summary>
    /// Добавить отмеченные в реестре записки в избранное (панель массовых действий).
    /// Записки, которые пользователю не видны или не существуют, пропускаются и перечисляются в Failed.
    /// </summary>
    [HttpPost("~/api/sz/favorites/bulk-add")]
    [ProducesResponseType(typeof(SzBulkResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddMany([FromBody] SzBulkIdsRequest request)
    {
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count > BulkLimit)
            return BadRequest(new { message = $"За раз можно отметить не больше {BulkLimit} записок" });

        var userId = _currentUser.UserId;
        var visible = await _sz.VisibleIdsAsync(ids, userId);
        await _favorites.AddManyAsync(visible, userId);

        var visibleSet = visible.ToHashSet();
        return Ok(new SzBulkResult
        {
            Requested = ids.Count,
            Succeeded = visible.Count,
            Failed = ids
                .Where(id => !visibleSet.Contains(id))
                .Select(id => new SzBulkFailure { SzId = id, Message = "Записка не найдена или нет доступа" })
                .ToList(),
        });
    }

    /// <summary>Убрать отмеченные записки из избранного. Снять свою отметку можно всегда.</summary>
    [HttpPost("~/api/sz/favorites/bulk-remove")]
    [ProducesResponseType(typeof(SzBulkResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveMany([FromBody] SzBulkIdsRequest request)
    {
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count > BulkLimit)
            return BadRequest(new { message = $"За раз можно убрать не больше {BulkLimit} записок" });

        await _favorites.RemoveManyAsync(ids, _currentUser.UserId);
        return Ok(new SzBulkResult { Requested = ids.Count, Succeeded = ids.Count });
    }

    /// <summary>Убрать записку из избранного текущего пользователя.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(int id)
    {
        // Снять свою отметку можно всегда — даже если доступ к записке уже пропал.
        await _favorites.RemoveAsync(id, _currentUser.UserId);
        return NoContent();
    }
}
