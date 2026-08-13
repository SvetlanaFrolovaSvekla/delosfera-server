using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>
/// Запуск закупки по служебной записке (PRC-01). Полноценный закупочный контур
/// появится отдельным модулем; здесь — передача записки и связь документов.
/// </summary>
[ApiController]
[Route("api/sz")]
[Tags("СЗ — Закупки")]
[Authorize]
public class SzProcurementController : ControllerBase
{
    private readonly ISzProcurementService _procurement;
    private readonly ICurrentUserService _currentUser;

    public SzProcurementController(ISzProcurementService procurement, ICurrentUserService currentUser)
    {
        _procurement = procurement;
        _currentUser = currentUser;
    }

    /// <summary>Состояние передачи: реквизиты заявки и что мешает её запустить.</summary>
    [HttpGet("{id:int}/procurement")]
    public async Task<IActionResult> Get(int id) => await Run(() => _procurement.GetAsync(id));

    /// <summary>Запустить закупку по записке.</summary>
    [HttpPost("{id:int}/procurement")]
    public async Task<IActionResult> HandOver(int id, [FromBody] SzProcurementHandoffRequest req) =>
        await Run(() => _procurement.HandOverAsync(id, req, _currentUser.UserId));

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
