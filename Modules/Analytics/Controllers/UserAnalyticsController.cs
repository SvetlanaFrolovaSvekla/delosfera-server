using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.DTO.Response;
using delosfera_server.Modules.Analytics.DTO.Response.Users;
using delosfera_server.Modules.Analytics.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Analytics.Controllers;

/// <summary>
/// Аналитика по модулю "Пользователи" для страницы отчётности: сводки по составу и вовлечённости
/// пользователей, а также их активность в связанных модулях (например, в ВНД — кто больше всех
/// инициирует документы и как быстро согласующие принимают решения).
/// </summary>
[ApiController]
[Route("/analytics/users")]
[Tags("Аналитика — Пользователи")]
[Authorize]
[RequirePermission(PermissionCode.ViewFullStatistics)]
public class UserAnalyticsController : ControllerBase
{
    private readonly IUserAnalyticsService _service;
    private readonly ILanguageResolver _languageResolver;

    public UserAnalyticsController(IUserAnalyticsService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>KPI-плашки: всего пользователей, активных, вовлечённость, число ролей и подразделений</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(UserOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserOverviewResponse>> GetOverview()
        => Ok(await _service.GetOverviewAsync());

    /// <summary>Динамика регистраций новых пользователей по периодам (линейный график)</summary>
    [HttpPost("registration-trend")]
    [ProducesResponseType(typeof(List<ChartTimePoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartTimePoint>>> GetRegistrationTrend([FromBody] AnalyticsPeriodRequest request)
        => Ok(await _service.GetRegistrationTrendAsync(request));

    /// <summary>Распределение пользователей по ролям</summary>
    [HttpGet("role-distribution")]
    [ProducesResponseType(typeof(List<ChartCategoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartCategoryPoint>>> GetRoleDistribution()
        => Ok(await _service.GetRoleDistributionAsync(_languageResolver.Resolve(Request)));

    /// <summary>Распределение пользователей по подразделениям (топ N + "Остальные")</summary>
    /// <param name="top">Сколько подразделений показать отдельно. По умолчанию 10</param>
    [HttpGet("org-unit-distribution")]
    [ProducesResponseType(typeof(List<ChartCategoryPoint>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChartCategoryPoint>>> GetOrgUnitDistribution([FromQuery] int top = 10)
        => Ok(await _service.GetOrgUnitDistributionAsync(_languageResolver.Resolve(Request), top));

    /// <summary>Вовлечённость: распределение пользователей по давности последнего входа
    /// (сегодня / 7 дней / 30 дней / 90 дней / давно / никогда)</summary>
    [HttpGet("activity-buckets")]
    [ProducesResponseType(typeof(UserActivityBucketsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserActivityBucketsResponse>> GetActivityBuckets()
        => Ok(await _service.GetActivityBucketsAsync());

    /// <summary>Топ пользователей по количеству созданных и актуализируемых ВНД</summary>
    /// <param name="top">Сколько пользователей показать. По умолчанию 10</param>
    [HttpGet("top-initiators")]
    [ProducesResponseType(typeof(List<UserTopInitiatorItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserTopInitiatorItem>>> GetTopInitiators([FromQuery] int top = 10)
        => Ok(await _service.GetTopInitiatorsAsync(top));

    /// <summary>Персональная скорость принятия решений согласующими пользователями по ВНД
    /// (для рейтинга самых быстрых/медленных согласующих)</summary>
    /// <param name="top">Сколько пользователей показать. По умолчанию 20</param>
    [HttpGet("approver-performance")]
    [ProducesResponseType(typeof(List<UserApproverPerformanceItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserApproverPerformanceItem>>> GetApproverPerformance([FromQuery] int top = 20)
        => Ok(await _service.GetApproverPerformanceAsync(top));
}
