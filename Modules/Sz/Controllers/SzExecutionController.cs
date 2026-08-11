using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Services;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>
/// Исполнение служебных записок (срез 3): резолюция руководителя, поручения
/// исполнителям, отчёты, продление срока и закрытие записки.
/// </summary>
[ApiController]
[Route("api/sz")]
[Tags("СЗ — Исполнение")]
[Authorize]
public class SzExecutionController : ControllerBase
{
    private readonly ISzExecutionService _execution;
    private readonly ICurrentUserService _currentUser;

    public SzExecutionController(ISzExecutionService execution, ICurrentUserService currentUser)
    {
        _execution = execution;
        _currentUser = currentUser;
    }

    /// <summary>Очередь «Мои поручения» — незакрытые поручения текущего пользователя.</summary>
    [HttpGet("assignments/my")]
    public async Task<IActionResult> My([FromQuery] bool includeClosed = false) =>
        Ok(await _execution.MyAsync(_currentUser.UserId, includeClosed));

    /// <summary>Поручения по записке.</summary>
    [HttpGet("{id:int}/assignments")]
    public async Task<IActionResult> List(int id) => Ok(await _execution.ListAsync(id));

    /// <summary>Резолюция руководителя: текст + поручения исполнителям.</summary>
    [HttpPost("{id:int}/resolution")]
    public async Task<IActionResult> Resolve(int id, [FromBody] SzResolutionRequest req) =>
        await Run(() => _execution.ResolveAsync(id, req, _currentUser.UserId));

    /// <summary>Сдать отчёт по поручению.</summary>
    [HttpPost("assignments/{assignmentId:int}/report")]
    public async Task<IActionResult> Report(int assignmentId, [FromBody] SzReportRequest req) =>
        await Run(() => _execution.ReportAsync(assignmentId, req.ReportText, _currentUser.UserId));

    /// <summary>Принять отчёт: закрытие последнего поручения переводит записку в «Исполнена».</summary>
    [HttpPost("assignments/{assignmentId:int}/accept")]
    public async Task<IActionResult> Accept(int assignmentId) =>
        await Run(() => _execution.AcceptAsync(assignmentId, _currentUser.UserId));

    /// <summary>Вернуть отчёт исполнителю с причиной.</summary>
    [HttpPost("assignments/{assignmentId:int}/return")]
    public async Task<IActionResult> Return(int assignmentId, [FromBody] SzReturnRequest req) =>
        await Run(() => _execution.ReturnAsync(assignmentId, req.Reason, _currentUser.UserId));

    /// <summary>Снять поручение.</summary>
    [HttpPost("assignments/{assignmentId:int}/cancel")]
    public async Task<IActionResult> Cancel(int assignmentId) =>
        await Run(() => _execution.CancelAsync(assignmentId, _currentUser.UserId));

    /// <summary>Продлить срок исполнения записки с обоснованием.</summary>
    [HttpPost("{id:int}/extend")]
    public async Task<IActionResult> Extend(int id, [FromBody] SzExtendDueDateRequest req) =>
        await Run(async () =>
        {
            await _execution.ExtendDueDateAsync(id, req.DueDate, req.Reason, _currentUser.UserId);
            return true;
        });

    /// <summary>Отметить записку исполненной.</summary>
    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] SzCompleteRequest req) =>
        await Run(async () =>
        {
            await _execution.CompleteAsync(id, req.Summary, _currentUser.UserId);
            return true;
        });

    /// <summary>Ошибки правил контура одинаковы во всех действиях исполнения.</summary>
    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
