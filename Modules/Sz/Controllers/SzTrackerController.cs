using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>Доска служебных записок по стадиям (РС-4). Открыта всем: каждый видит
/// записки по тем же правилам, что и в реестре (<see cref="SzVisibility"/>).</summary>
[ApiController]
[Route("api/sz/tracker")]
[Tags("СЗ — Доска")]
[Authorize]
public class SzTrackerController : ControllerBase
{
    private readonly ISzTrackerService _tracker;
    private readonly ICurrentUserService _currentUser;

    public SzTrackerController(ISzTrackerService tracker, ICurrentUserService currentUser)
    {
        _tracker = tracker;
        _currentUser = currentUser;
    }

    /// <summary>Колонки по стадиям с записками.</summary>
    [HttpGet]
    public async Task<IActionResult> Board() => Ok(await _tracker.GetBoardAsync(_currentUser.UserId));
}
