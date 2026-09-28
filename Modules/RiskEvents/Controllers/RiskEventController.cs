using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.RiskEvents.DTO;
using delosfera_server.Modules.RiskEvents.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.RiskEvents.Controllers;

/// <summary>
/// События операционного риска (RISK-1, 716-П / НБКР). Поднимаются с любого шага
/// согласования или карточки документа; риск-подразделение ведёт по ним реестр и
/// разбор. Просмотр — по праву ViewRiskEvents, поднятие и смена статуса — по
/// ManageRiskEvents.
/// </summary>
[ApiController]
[Authorize]
[Route("api/risk-events")]
[Tags("События операционного риска")]
public class RiskEventController : ControllerBase
{
    private readonly IRiskEventService _service;
    private readonly ICurrentUserService _currentUser;

    public RiskEventController(IRiskEventService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Поднять событие оп-риска с шага согласования или карточки документа.</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageRiskEvents)]
    public async Task<IActionResult> Raise([FromBody] RaiseRiskEventRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.RaiseAsync(request, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Реестр событий с фильтрами (тип, серьёзность, статус, период, документ, поиск).</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ViewRiskEvents, PermissionCode.ManageRiskEvents)]
    public async Task<IActionResult> Search([FromQuery] RiskEventFilter filter, CancellationToken ct) =>
        Ok(await _service.SearchAsync(filter, ct));

    /// <summary>Карточка события.</summary>
    [HttpGet("{id:int}")]
    [RequirePermission(PermissionCode.ViewRiskEvents, PermissionCode.ManageRiskEvents)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var dto = await _service.GetAsync(id, ct);
        return dto is null ? NotFound(new { message = "Событие операционного риска не найдено" }) : Ok(dto);
    }

    /// <summary>Сменить статус события (новое → в работе → закрыто) и уточнить прямые потери.</summary>
    [HttpPatch("{id:int}/status")]
    [RequirePermission(PermissionCode.ManageRiskEvents)]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeRiskEventStatusRequest request, CancellationToken ct)
    {
        try { return Ok(await _service.ChangeStatusAsync(id, request, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
