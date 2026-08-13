using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>
/// Бумажный контур служебных записок (SZ-PAP): печатная форма с листом согласования
/// и контроль возврата бумажного оригинала.
/// </summary>
[ApiController]
[Route("api/sz")]
[Tags("СЗ — Бумажный контур")]
[Authorize]
public class SzPaperController : ControllerBase
{
    private readonly ISzPaperService _paper;
    private readonly ICurrentUserService _currentUser;

    public SzPaperController(ISzPaperService paper, ICurrentUserService currentUser)
    {
        _paper = paper;
        _currentUser = currentUser;
    }

    /// <summary>Печатная форма записки с листом согласования.</summary>
    [HttpGet("{id:int}/print")]
    public async Task<IActionResult> PrintForm(int id) => await Run(() => _paper.PrintFormAsync(id));

    /// <summary>Состояние бумажного оригинала.</summary>
    [HttpGet("{id:int}/original")]
    public async Task<IActionResult> Original(int id) => await Run(() => _paper.GetOriginalAsync(id));

    /// <summary>Выдать оригинал под контроль возврата.</summary>
    [HttpPost("{id:int}/original/handover")]
    public async Task<IActionResult> HandOver(int id, [FromBody] SzHandoverRequest req) =>
        await Run(() => _paper.HandOverAsync(id, req, _currentUser.UserId));

    /// <summary>Принять оригинал обратно.</summary>
    [HttpPost("{id:int}/original/return")]
    public async Task<IActionResult> Return(int id) =>
        await Run(() => _paper.ReturnAsync(id, _currentUser.UserId));

    /// <summary>Оригиналы на руках — реестр делопроизводства.</summary>
    [HttpGet("originals/outstanding")]
    public async Task<IActionResult> Outstanding([FromQuery] bool overdueOnly = false) =>
        await Run(() => _paper.OutstandingAsync(overdueOnly));

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
