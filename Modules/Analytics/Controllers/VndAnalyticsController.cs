using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.DTO.Response;
using delosfera_server.Modules.Analytics.DTO.Response.Vnd;
using delosfera_server.Modules.Analytics.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Analytics.Controllers;

/// <summary>
/// Аналитика по модулю ВНД для страницы отчётности
/// </summary>
[ApiController]
[Route("/analytics/vnd")]
[Tags("Аналитика - ВНД")]
[Authorize]
[RequirePermission(PermissionCode.ViewFullStatistics)]
public class VndAnalyticsController : ControllerBase
{
    private readonly IVndAnalyticsService _service;
    private readonly ILanguageResolver _languageResolver;

    public VndAnalyticsController(IVndAnalyticsService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>KPI для верхней части страницы отчётности: сколько ВНД в каждом статусе,
    /// сколько просрочено по актуализации, сколько согласований идёт прямо сейчас, средние сроки</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(VndOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndOverviewResponse>> GetOverview()
        => Ok(await _service.GetOverviewAsync());

    /// <summary>Круговая диаграмма: распределение ВНД по статусам жизненного цикла</summary>
    [HttpGet("status-distribution")]
    [ProducesResponseType(typeof(List<ChartCategoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartCategoryPoint>>> GetStatusDistribution()
        => Ok(await _service.GetStatusDistributionAsync(_languageResolver.Resolve(Request)));

    /// <summary>Столбчатая/круговая диаграмма: распределение ВНД по видам (типам) документа</summary>
    [HttpGet("type-distribution")]
    [ProducesResponseType(typeof(List<ChartCategoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartCategoryPoint>>> GetTypeDistribution()
        => Ok(await _service.GetTypeDistributionAsync(_languageResolver.Resolve(Request)));

    /// <summary>Топ подразделений-разработчиков по количеству ВНД (остальные объединяются в "Остальные")</summary>
    /// <param name="top">Сколько подразделений показать отдельно, остальные суммируются. По умолчанию 10</param>
    [HttpGet("developer-distribution")]
    [ProducesResponseType(typeof(List<ChartCategoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartCategoryPoint>>> GetDeveloperDistribution([FromQuery] int top = 10)
        => Ok(await _service.GetDeveloperDistributionAsync(_languageResolver.Resolve(Request), top));

    /// <summary>Распределение ВНД по уровням секретности</summary>
    [HttpGet("security-level-distribution")]
    [ProducesResponseType(typeof(List<ChartCategoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartCategoryPoint>>> GetSecurityLevelDistribution()
        => Ok(await _service.GetSecurityLevelDistributionAsync(_languageResolver.Resolve(Request)));

    /// <summary>Топ рубрик классификатора по количеству привязанных ВНД</summary>
    /// <param name="top">Сколько рубрик показать. По умолчанию 10</param>
    [HttpGet("rubric-distribution")]
    [ProducesResponseType(typeof(List<ChartCategoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartCategoryPoint>>> GetRubricDistribution([FromQuery] int top = 10)
        => Ok(await _service.GetRubricDistributionAsync(_languageResolver.Resolve(Request), top));

    /// <summary>Облако ключевых слов: топ самых часто используемых ключевых слов в ВНД</summary>
    /// <param name="top">Сколько ключевых слов показать. По умолчанию 30</param>
    [HttpGet("keyword-cloud")]
    [ProducesResponseType(typeof(List<ChartCategoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartCategoryPoint>>> GetKeywordCloud([FromQuery] int top = 30)
        => Ok(await _service.GetKeywordCloudAsync(_languageResolver.Resolve(Request), top));

    /// <summary>Линейный/столбчатый график динамики жизненного цикла ВНД по периодам:
    /// сколько создано, отправлено на согласование, опубликовано и архивировано</summary>
    [HttpPost("dynamics")]
    [ProducesResponseType(typeof(List<VndDynamicsPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndDynamicsPoint>>> GetDynamics([FromBody] AnalyticsPeriodRequest request)
        => Ok(await _service.GetDynamicsAsync(request));

    /// <summary>График динамики циклов актуализации по периодам: запущено/опубликовано,
    /// доля с реальными изменениями, средняя длительность цикла</summary>
    [HttpPost("actualization-trend")]
    [ProducesResponseType(typeof(List<VndActualizationTrendPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndActualizationTrendPoint>>> GetActualizationTrend([FromBody] AnalyticsPeriodRequest request)
        => Ok(await _service.GetActualizationTrendAsync(request));

    /// <summary>Эффективность процесса согласования: доля успешных, доля с доработками,
    /// средняя и медианная длительность, тренд средней длительности по периодам</summary>
    [HttpPost("approval-performance")]
    [ProducesResponseType(typeof(VndApprovalPerformanceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndApprovalPerformanceResponse>> GetApprovalPerformance([FromBody] AnalyticsPeriodRequest? request)
        => Ok(await _service.GetApprovalPerformanceAsync(request));

    /// <summary>Загрузка согласующих подразделений - сколько этапов согласования прошло через
    /// каждое подразделение, доля решений по таймауту (индикатор "узких мест" маршрута)</summary>
    [HttpGet("approver-workload")]
    [ProducesResponseType(typeof(List<VndApproverWorkloadItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndApproverWorkloadItem>>> GetApproverWorkloadByOrgUnit()
        => Ok(await _service.GetApproverWorkloadAsync(byUser: false));

    /// <summary>То же самое, но в разрезе конкретных согласующих пользователей, а не подразделений</summary>
    [HttpGet("approver-workload/by-user")]
    [ProducesResponseType(typeof(List<VndApproverWorkloadItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndApproverWorkloadItem>>> GetApproverWorkloadByUser()
        => Ok(await _service.GetApproverWorkloadAsync(byUser: true));

    /// <summary>Тепловая карта "подразделение-разработчик × статус ВНД"</summary>
    [HttpGet("org-unit-status-matrix")]
    [ProducesResponseType(typeof(List<VndOrgUnitStatusMatrixItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndOrgUnitStatusMatrixItem>>> GetOrgUnitStatusMatrix()
        => Ok(await _service.GetOrgUnitStatusMatrixAsync(_languageResolver.Resolve(Request)));

    /// <summary>Выгрузка сводного CSV-отчёта (KPI + основные распределения) с кнопки "Скачать отчёт"
    /// на странице отчётности. Требует отдельное право на экспорт статистики</summary>
    [HttpGet("export")]
    [RequirePermission(PermissionCode.ExportFullStatisticsReport)]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportCsv()
    {
        var bytes = await _service.ExportOverviewCsvAsync(_languageResolver.Resolve(Request));
        var fileName = $"vnd-report-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
