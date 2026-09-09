using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;
namespace delosfera_server.Modules.Documents.VND.Controllers;

[ApiController]
[Route("api/tasks")]
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

    /// <summary>"Ждущие моего согласования" — первичное, повторное согласование и финальная
    /// выдержка одним списком (см. TasksService.GetCoordinationTasksAsync).</summary>
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

    /// <summary>"Отклонено" — редакции, отклонённые при согласовании и ожидающие правок
    /// инициатора (см. TasksService.GetRejectedTasksAsync).</summary>
    [HttpGet("rejected")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetRejected() =>
        Ok(await _service.GetRejectedTasksAsync(_currentUser.UserId));

    [HttpGet("counts")]
    public async Task<ActionResult<VndTaskCountsResponse>> GetCounts() =>
        Ok(await _service.GetCountsAsync(_currentUser.UserId));

    // --- История "Выполнено" по каждому разделу — см. TasksService для точного критерия
    // "выполнено" в каждом случае. page — с единицы, pageSize ограничен 1..100.

    [HttpGet("coordination/done")]
    public async Task<ActionResult<PagedResult<VndTaskResponse>>> GetCoordinationDone(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _service.GetCoordinationDoneTasksAsync(_currentUser.UserId, page, pageSize));

    [HttpGet("my-vnd-approval/done")]
    public async Task<ActionResult<PagedResult<VndTaskResponse>>> GetMyVndApprovalDone(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _service.GetMyVndApprovalDoneTasksAsync(_currentUser.UserId, page, pageSize));

    [HttpGet("actualization/done")]
    public async Task<ActionResult<PagedResult<VndTaskResponse>>> GetActualizationDone(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _service.GetActualizationDoneTasksAsync(_currentUser.UserId, page, pageSize));

    [HttpGet("consolidation/done")]
    public async Task<ActionResult<PagedResult<VndTaskResponse>>> GetConsolidationDone(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _service.GetConsolidationDoneTasksAsync(_currentUser.UserId, page, pageSize));

    [HttpGet("rejected/done")]
    public async Task<ActionResult<PagedResult<VndTaskResponse>>> GetRejectedDone(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _service.GetRejectedDoneTasksAsync(_currentUser.UserId, page, pageSize));

    /// <summary>Персональные KPI для карточек на главной странице</summary>
    [HttpGet("home-summary")]
    [ProducesResponseType(typeof(VndHomeSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndHomeSummaryResponse>> GetHomeSummary() =>
        Ok(await _service.GetHomeSummaryAsync(_currentUser.UserId));
}