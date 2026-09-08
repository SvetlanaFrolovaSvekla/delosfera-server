using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.ActivityLog.DTO.Response;
using delosfera_server.Modules.ActivityLog.Services;

namespace delosfera_server.Modules.ActivityLog.Controllers;

[ApiController]
[Route("api/activity-log")]
[Tags("Журнал активности")]
[Authorize]
public class ActivityLogController : ControllerBase
{
    private readonly IActivityLogService _service;
    private readonly IDocumentHistoryService _history;
    private readonly ILanguageResolver _languageResolver;

    public ActivityLogController(
        IActivityLogService service,
        IDocumentHistoryService history,
        ILanguageResolver languageResolver)
    {
        _service = service;
        _history = history;
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

    /// <summary>
    /// История действий по одному документу — из технического аудита. Работает
    /// для любого контура: записки, закупки, договора, письма — у всех аудит уже
    /// пишется, отдельного журнала не заводили.
    /// </summary>
    [HttpGet("history/{entityType}/{entityId:int}")]
    [ProducesResponseType(typeof(List<DocumentHistoryEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DocumentHistoryEntry>>> History(
        string entityType, int entityId,
        [FromQuery] int[]? assignmentIds,
        CancellationToken ct)
    {
        // Записка тянет за собой поручения: их события — часть её истории, но в
        // аудите лежат под своим типом и своими id.
        var related = (assignmentIds ?? [])
            .Select(id => ("SzAssignment", id))
            .ToList();

        return Ok(await _history.ForAsync(entityType, entityId, related, ct));
    }
}