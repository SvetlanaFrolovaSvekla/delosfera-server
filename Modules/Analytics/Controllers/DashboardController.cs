using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Analytics.Services;

namespace delosfera_server.Modules.Analytics.Controllers;

/// <summary>Рабочий стол: сводка по всем контурам для текущего пользователя (GEN-15).</summary>
[ApiController]
[Route("api/dashboard")]
[Tags("Рабочий стол")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(IDashboardService dashboard, ICurrentUserService currentUser)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
    }

    /// <summary>KPI-плитки и активные замещения.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary() =>
        Ok(await _dashboard.GetSummaryAsync(_currentUser.UserId));
}
