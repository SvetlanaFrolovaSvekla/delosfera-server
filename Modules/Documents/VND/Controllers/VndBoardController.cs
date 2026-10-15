using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>"Доска ВНД": документы, связанные с СП пользователя, плюс добавленные им вручную,
/// разложенные по статусу последней редакции (раскладка — на фронте).</summary>
[ApiController]
[Route("api/vnd/board")]
[Tags("ВНД — Доска")]
[Authorize]
public class VndBoardController : ControllerBase
{
    private readonly IVndBoardService _service;
    private readonly ILanguageResolver _languageResolver;

    public VndBoardController(IVndBoardService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>Доска текущего пользователя.</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(VndBoardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndBoardResponse>> Get()
    {
        var language = _languageResolver.Resolve(Request);
        return Ok(await _service.GetAsync(language));
    }

    /// <summary>Поставить документ на доску (закрепить вручную или вернуть скрытый).</summary>
    [HttpPut("{vndId:int}")]
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

    /// <summary>Убрать документ с доски (открепить или скрыть связанный с СП).</summary>
    [HttpDelete("{vndId:int}")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(int vndId)
    {
        await _service.RemoveAsync(vndId);
        return NoContent();
    }
}
