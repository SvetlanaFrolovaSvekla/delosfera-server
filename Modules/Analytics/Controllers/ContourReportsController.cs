using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Analytics.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Analytics.Controllers;

/// <summary>Отчёты по контурам (АН-1..4): закупки, заседания, кадры, канцелярия.</summary>
[ApiController]
[Route("api/analytics/contour")]
[Tags("Аналитика — Контуры")]
[Authorize]
[RequirePermission(PermissionCode.ViewFullStatistics)]
public class ContourReportsController : ControllerBase
{
    private readonly IContourReportsService _reports;

    public ContourReportsController(IContourReportsService reports)
    {
        _reports = reports;
    }

    [HttpGet("procurement")]
    public async Task<IActionResult> Procurement() => Ok(await _reports.ProcurementAsync());

    [HttpGet("meetings")]
    public async Task<IActionResult> Meetings() => Ok(await _reports.MeetingsAsync());

    [HttpGet("hr")]
    public async Task<IActionResult> Hr() => Ok(await _reports.HrAsync());

    [HttpGet("office")]
    public async Task<IActionResult> Office() => Ok(await _reports.OfficeAsync());
}
