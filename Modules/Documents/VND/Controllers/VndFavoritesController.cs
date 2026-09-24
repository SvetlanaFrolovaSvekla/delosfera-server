using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>Звёздочка "Избранное" на странице ВНД. Список избранного — вкладка "Избранное" в
/// реестре (POST api/vnd/search с FavoritesOnly = true), признак — VndResponse.IsFavorite.</summary>
[ApiController]
[Route("api/vnd/{vndId:int}/favorite")]
[Tags("ВНД — Избранное")]
[Authorize]
public class VndFavoritesController : ControllerBase
{
    private readonly IVndFavoriteService _service;

    public VndFavoritesController(IVndFavoriteService service)
    {
        _service = service;
    }

    /// <summary>Добавить документ в избранное текущего пользователя.</summary>
    [HttpPut]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(int vndId)
    {
        try
        {
            await _service.AddAsync(vndId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Убрать документ из избранного текущего пользователя.</summary>
    [HttpDelete]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(int vndId)
    {
        await _service.RemoveAsync(vndId);
        return NoContent();
    }
}
