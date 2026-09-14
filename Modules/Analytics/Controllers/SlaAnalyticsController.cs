using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Analytics.DTO.Response.Sla;
using delosfera_server.Modules.Analytics.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Analytics.Controllers;

/// <summary>
/// SLA-аналитика: соблюдение сроков по всем контурам сразу, для руководства (СК-2).
/// Срез общебанковский, поэтому закрыт правом просмотра полной статистики.
/// </summary>
[ApiController]
[Route("api/analytics/sla")]
[Tags("Аналитика - Сроки (SLA)")]
[Authorize]
[RequirePermission(PermissionCode.ViewFullStatistics)]
public class SlaAnalyticsController : ControllerBase
{
    private readonly ISlaAnalyticsService _service;

    public SlaAnalyticsController(ISlaAnalyticsService service)
    {
        _service = service;
    }

    /// <summary>KPI-плашки и разбивка соблюдения сроков по контурам.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<SlaOverviewResponse>> GetOverview() =>
        Ok(await _service.GetOverviewAsync());

    /// <summary>Сотрудники с наибольшим числом просроченных задач.</summary>
    [HttpGet("top-overdue")]
    public async Task<ActionResult<List<SlaViolatorItem>>> GetTopOverdue([FromQuery] int top = 15) =>
        Ok(await _service.GetTopOverdueAsync(top));
}
