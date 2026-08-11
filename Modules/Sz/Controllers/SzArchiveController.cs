using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Services;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>
/// Архивное хранение служебных записок (SZ-07, GEN-09): подшивка в дело номенклатуры,
/// сроки хранения и опись дела.
/// </summary>
[ApiController]
[Route("api/sz")]
[Tags("СЗ — Архив")]
[Authorize]
public class SzArchiveController : ControllerBase
{
    private readonly ISzArchiveService _archive;
    private readonly ICurrentUserService _currentUser;

    public SzArchiveController(ISzArchiveService archive, ICurrentUserService currentUser)
    {
        _archive = archive;
        _currentUser = currentUser;
    }

    /// <summary>Карточка архивного хранения записки.</summary>
    [HttpGet("{id:int}/archive")]
    public async Task<IActionResult> Get(int id) => await Run(() => _archive.GetAsync(id));

    /// <summary>Подшить записку в дело и перевести в архив.</summary>
    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id, [FromBody] SzArchiveRequest req) =>
        await Run(() => _archive.ArchiveAsync(id, req, _currentUser.UserId));

    /// <summary>Вернуть записку из архива.</summary>
    [HttpPost("{id:int}/archive/restore")]
    public async Task<IActionResult> Restore(int id) =>
        await Run(() => _archive.RestoreAsync(id, _currentUser.UserId));

    /// <summary>Опись дела: что в нём подшито и до какого года хранится.</summary>
    [HttpGet("archive/cases/{caseId:int}/inventory")]
    public async Task<IActionResult> Inventory(int caseId) =>
        await Run(() => _archive.CaseInventoryAsync(caseId));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
