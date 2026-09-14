using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>Доска служебных записок по стадиям (РС-4). Общая по банку — закрыта правом
/// просмотра всех записок.</summary>
[ApiController]
[Route("api/sz/tracker")]
[Tags("СЗ — Доска")]
[Authorize]
[RequirePermission(PermissionCode.ViewAllSz)]
public class SzTrackerController : ControllerBase
{
    private readonly ISzTrackerService _tracker;

    public SzTrackerController(ISzTrackerService tracker)
    {
        _tracker = tracker;
    }

    /// <summary>Колонки по стадиям с записками.</summary>
    [HttpGet]
    public async Task<IActionResult> Board() => Ok(await _tracker.GetBoardAsync());
}
