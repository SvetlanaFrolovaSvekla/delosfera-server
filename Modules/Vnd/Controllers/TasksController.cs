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

    /// <summary>Задачи текущего пользователя по согласованию редакций ВНД</summary>
    [HttpGet("coordination")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetCoordination() =>
        Ok(await _service.GetCoordinationTasksAsync(_currentUser.UserId));

    /// <summary>ВНД в статусе актуализации, где пользователь причастен</summary>
    [HttpGet("actualization")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetActualization() =>
        Ok(await _service.GetActualizationTasksAsync(_currentUser.UserId));

    /// <summary>ВНД в статусе консолидации, где пользователь причастен</summary>
    [HttpGet("consolidation")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetConsolidation() =>
        Ok(await _service.GetConsolidationTasksAsync(_currentUser.UserId));

    /// <summary>Мои ВНД на согласовании — где пользователь инициатор согласования
    /// или ответственный за актуализацию (но не согласующий)</summary>
    [HttpGet("my-vnd-approval")]
    public async Task<ActionResult<List<VndTaskResponse>>> GetMyVndApproval() =>
        Ok(await _service.GetMyVndApprovalTasksAsync(_currentUser.UserId));

    /// <summary>Счётчики задач текущего пользователя по всем разделам</summary>
    [HttpGet("counts")]
    public async Task<ActionResult<VndTaskCountsResponse>> GetCounts() =>
        Ok(await _service.GetCountsAsync(_currentUser.UserId));
}