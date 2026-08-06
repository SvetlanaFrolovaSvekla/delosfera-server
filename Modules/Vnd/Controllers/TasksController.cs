using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Services;

namespace delosfera_server.Modules.Tasks.Controllers;

[ApiController]
[Route("/tasks")]
[Tags("Мои задачи")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITasksService _service;
    private readonly ICurrentUserService _currentUser;

    public TasksController(ITasksService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("coordination")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetCoordination() =>
        Ok(await _service.GetCoordinationTasksAsync(_currentUser.UserId));

    [HttpGet("actualization")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetActualization() =>
        Ok(await _service.GetActualizationTasksAsync(_currentUser.UserId));

    [HttpGet("consolidation")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetConsolidation() =>
        Ok(await _service.GetConsolidationTasksAsync(_currentUser.UserId));

    [HttpGet("my-vnd-approval")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetMyVndApproval() =>
        Ok(await _service.GetMyVndApprovalTasksAsync(_currentUser.UserId));

    [HttpGet("counts")]
    public async Task<ActionResult<VndTaskCountsResponse>> GetCounts() =>
        Ok(await _service.GetCountsAsync(_currentUser.UserId));

    /// <summary>Персональные KPI для карточек на главной странице</summary>
    [HttpGet("home-summary")]
    [ProducesResponseType(typeof(VndHomeSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndHomeSummaryResponse>> GetHomeSummary() =>
        Ok(await _service.GetHomeSummaryAsync(_currentUser.UserId));
}