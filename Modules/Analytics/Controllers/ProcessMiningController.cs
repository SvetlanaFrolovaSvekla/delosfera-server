using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.DTO.Response.ProcessMining;
using delosfera_server.Modules.Analytics.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Analytics.Controllers;

/// <summary>
/// Process mining по трейсам маршрутов согласования (MINE-1): фактические варианты
/// маршрута, возвраты/доработки и узкие места. Право то же, что и у остальной
/// расширенной аналитики — просмотр полной статистики.
/// </summary>
[ApiController]
[Route("api/analytics/process-mining")]
[Tags("Аналитика - Process mining")]
[Authorize]
[RequirePermission(PermissionCode.ViewFullStatistics)]
public class ProcessMiningController : ControllerBase
{
    private readonly IProcessMiningService _service;

    public ProcessMiningController(IProcessMiningService service) => _service = service;

    /// <summary>
    /// Сводный отчёт: KPI, варианты маршрута с частотами, возвраты по этапам,
    /// узкие места по этапам и участникам. Фильтры: тип документа, шаблон, период.
    /// </summary>
    [HttpPost("report")]
    [ProducesResponseType(typeof(ProcessMiningReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcessMiningReport>> GetReport([FromBody] ProcessMiningFilter? filter)
        => Ok(await _service.GetReportAsync(filter ?? new ProcessMiningFilter()));

    /// <summary>Доступные значения фильтров (типы документов и шаблоны маршрутов в данных).</summary>
    [HttpGet("filters")]
    [ProducesResponseType(typeof(ProcessMiningFilterOptions), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcessMiningFilterOptions>> GetFilters()
        => Ok(await _service.GetFilterOptionsAsync());
}
