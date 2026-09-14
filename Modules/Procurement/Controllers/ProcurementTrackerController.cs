using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Доска закупок по стадиям (ЗК-11): активные заявки, разложенные по жизненному циклу.
/// Общая по банку картина, поэтому закрыта правом просмотра всех закупок.
/// </summary>
[ApiController]
[Route("api/procurement/tracker")]
[Tags("Закупки — Доска")]
[Authorize]
[RequirePermission(PermissionCode.ViewAllProcurements)]
public class ProcurementTrackerController : ControllerBase
{
    private readonly IProcurementTrackerService _tracker;

    public ProcurementTrackerController(IProcurementTrackerService tracker)
    {
        _tracker = tracker;
    }

    /// <summary>Колонки по стадиям с заявками.</summary>
    [HttpGet]
    public async Task<IActionResult> Board() => Ok(await _tracker.GetBoardAsync());
}
