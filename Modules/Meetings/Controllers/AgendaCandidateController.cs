using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;

namespace delosfera_server.Modules.Meetings.Controllers;

/// <summary>Включение заявки на закупку в повестку.</summary>
public class AgendaFromProcurementRequest
{
    public int RequestId { get; set; }

    /// <summary>Формулировка вопроса. Пусто — берётся предмет закупки.</summary>
    public string? Question { get; set; }

    /// <summary>Место в повестке. Пусто — в конец.</summary>
    public int? Order { get; set; }
}

public class TakeIntoAgendaRequest
{
    public int SzId { get; set; }

    /// <summary>Формулировка вопроса. Пусто — берётся предложенная автором или тема записки.</summary>
    public string? Question { get; set; }

    /// <summary>Место в повестке. Пусто — в конец.</summary>
    public int? Order { get; set; }
}

/// <summary>
/// Отбор служебных записок в повестку.
///
/// Сотрудник ставит на записке отметку «вынести на Правление» — и она встаёт в
/// очередь к секретарю этого органа. Секретарь решает, включать ли её, на какое
/// заседание и под какой формулировкой: повестку органа определяет он, а не тот,
/// кто поставил галочку.
/// </summary>
[ApiController]
[Authorize]
[Route("api/meetings/candidates")]
[Tags("Заседания — отбор вопросов")]
public class AgendaCandidateController : ControllerBase
{
    private readonly IAgendaCandidateService _candidates;
    private readonly IMeetingAccessService _access;
    private readonly ICurrentUserService _currentUser;

    public AgendaCandidateController(
        IAgendaCandidateService candidates,
        IMeetingAccessService access,
        ICurrentUserService currentUser)
    {
        _candidates = candidates;
        _access = access;
        _currentUser = currentUser;
    }

    /// <summary>Очередь записок, ожидающих отбора в повестку органа.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] MeetingBody body, CancellationToken ct)
    {
        if (!_access.CanManage(body))
            return Forbid();

        return Ok(await _candidates.ListAsync(body, ct));
    }

    /// <summary>Включить записку в повестку заседания отдельным вопросом.</summary>
    [HttpPost("/api/meetings/{meetingId:int}/agenda/from-sz")]
    public async Task<IActionResult> Take(
        int meetingId, [FromBody] TakeIntoAgendaRequest request, CancellationToken ct)
    {
        try
        {
            var item = await _candidates.TakeIntoAgendaAsync(
                meetingId, request.SzId, request.Question, request.Order, _currentUser.UserId, ct);

            return Ok(new { id = item.Id, item.Order, item.Topic });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Включить заявку на закупку в повестку заседания.</summary>
    [HttpPost("/api/meetings/{meetingId:int}/agenda/from-procurement")]
    public async Task<IActionResult> FromProcurement(
        int meetingId, [FromBody] AgendaFromProcurementRequest request, CancellationToken ct)
    {
        try
        {
            var item = await _candidates.TakeProcurementIntoAgendaAsync(
                meetingId, request.RequestId, request.Question, request.Order, _currentUser.UserId, ct);

            return Ok(new {item.Id, item.Order, item.Topic});
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new {message = ex.Message});
        }
    }

    /// <summary>Отклонить заявку: снять с записки отметку о вынесении на орган.</summary>
    [HttpPost("{szId:int}/decline")]
    public async Task<IActionResult> Decline(int szId, [FromQuery] MeetingBody body, CancellationToken ct)
    {
        if (!_access.CanManage(body))
            return Forbid();

        try
        {
            await _candidates.DeclineAsync(szId, _currentUser.UserId, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
