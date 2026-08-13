using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.ActivityLog.DTO.Response;
using delosfera_server.Modules.ActivityLog.Services;

namespace delosfera_server.Modules.ActivityLog.Controllers;

[ApiController]
[Route("/activity-log")]
[Tags("Журнал активности")]
[Authorize]
public class ActivityLogController : ControllerBase
{
    private readonly IActivityLogService _service;
    private readonly ILanguageResolver _languageResolver;

    public ActivityLogController(IActivityLogService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>Последние события по всем модулям (или по одному, если передан module) - для дашборда</summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<ActivityLogEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActivityLogEntryResponse>>> GetRecent(
        [FromQuery] int limit = 8, [FromQuery] string? module = null)
    {
        var language = _languageResolver.Resolve(Request);
        return Ok(await _service.GetRecentAsync(Math.Clamp(limit, 1, 50), language, module));
    }
}