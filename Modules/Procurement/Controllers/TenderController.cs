using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Controllers;

/// <summary>
/// Конкурс по закупке (PRC-13..16): комиссия, публикация, приём и вскрытие заявок,
/// оценка и определение победителя.
/// </summary>
[ApiController]
[Route("api/procurement")]
[Tags("Закупки — Конкурс")]
[Authorize]
public class TenderController : ControllerBase
{
    private readonly ITenderService _tenders;
    private readonly ICurrentUserService _currentUser;

    public TenderController(ITenderService tenders, ICurrentUserService currentUser)
    {
        _tenders = tenders;
        _currentUser = currentUser;
    }

    /// <summary>Конкурс по заявке; 204, если не объявлялся.</summary>
    [HttpGet("requests/{id:int}/tender")]
    public async Task<IActionResult> Get(int id)
    {
        var tender = await _tenders.GetAsync(id);
        return tender is null ? NoContent() : Ok(tender);
    }

    /// <summary>Начать подготовку конкурса по заявке.</summary>
    [HttpPost("requests/{id:int}/tender")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Create(int id, [FromBody] TenderCreateRequest request) =>
        await Run(() => _tenders.CreateAsync(id, request, _currentUser.UserId));

    /// <summary>Включить сотрудника в комиссию.</summary>
    [HttpPost("tenders/{tenderId:int}/commission")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> AddMember(int tenderId, [FromBody] CommissionMemberRequest request) =>
        await Run(() => _tenders.AddMemberAsync(tenderId, request, _currentUser.UserId));

    /// <summary>Исключить сотрудника из комиссии.</summary>
    [HttpDelete("commission/{memberId:int}")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> RemoveMember(int memberId) =>
        await Run(() => _tenders.RemoveMemberAsync(memberId, _currentUser.UserId));

    /// <summary>Отметить участие в заседании и особое мнение.</summary>
    [HttpPost("commission/{memberId:int}/attendance")]
    [RequirePermission(PermissionCode.RecordCommissionDecisions)]
    public async Task<IActionResult> Attendance(int memberId, [FromBody] AttendanceRequest request) =>
        await Run(() => _tenders.SetAttendanceAsync(memberId, request, _currentUser.UserId));

    /// <summary>Заключение эксперта комиссии: текст и приложенный файл.</summary>
    [HttpPost("commission/{memberId:int}/conclusion")]
    [RequirePermission(PermissionCode.RecordCommissionDecisions)]
    public async Task<IActionResult> Conclusion(int memberId, [FromBody] ExpertConclusionRequest request) =>
        await Run(() => _tenders.SetConclusionAsync(memberId, request, _currentUser.UserId));

    /// <summary>Объявить конкурс: публикация и срок приёма заявок.</summary>
    [HttpPost("tenders/{tenderId:int}/publish")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Publish(int tenderId, [FromBody] TenderPublishRequest request) =>
        await Run(() => _tenders.PublishAsync(tenderId, request, _currentUser.UserId));

    /// <summary>Зарегистрировать конкурсную заявку поставщика.</summary>
    [HttpPost("tenders/{tenderId:int}/bids")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> AddBid(int tenderId, [FromBody] TenderBidRequest request) =>
        await Run(() => _tenders.AddBidAsync(tenderId, request, _currentUser.UserId));

    /// <summary>Вскрыть заявки после окончания срока приёма.</summary>
    [HttpPost("tenders/{tenderId:int}/open")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Open(int tenderId) =>
        await Run(() => _tenders.OpenBidsAsync(tenderId, _currentUser.UserId));

    /// <summary>Оценка заявки комиссией.</summary>
    [HttpPost("bids/{bidId:int}/score")]
    [RequirePermission(PermissionCode.RecordCommissionDecisions)]
    public async Task<IActionResult> Score(int bidId, [FromBody] BidScoreRequest request) =>
        await Run(() => _tenders.ScoreBidAsync(bidId, request, _currentUser.UserId));

    /// <summary>Назначить заседание комиссии или перенести его на другую дату.</summary>
    [HttpPost("tenders/{tenderId:int}/meeting")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Meeting(int tenderId, [FromBody] MeetingScheduleRequest request) =>
        await Run(() => _tenders.ScheduleMeetingAsync(tenderId, request, _currentUser.UserId));

    /// <summary>Внести результаты очного голосования комиссии по заявке.</summary>
    [HttpPost("bids/{bidId:int}/votes")]
    [RequirePermission(PermissionCode.RecordCommissionDecisions)]
    public async Task<IActionResult> Votes(int bidId, [FromBody] BidVotesRequest request) =>
        await Run(() => _tenders.RecordVotesAsync(bidId, request, _currentUser.UserId));

    /// <summary>Определить победителя конкурса.</summary>
    [HttpPost("tenders/{tenderId:int}/bids/{bidId:int}/winner")]
    [RequirePermission(PermissionCode.RecordCommissionDecisions)]
    public async Task<IActionResult> Winner(int tenderId, int bidId) =>
        await Run(() => _tenders.DeclareWinnerAsync(tenderId, bidId, _currentUser.UserId));

    /// <summary>Признать конкурс несостоявшимся или отменить его.</summary>
    [HttpPost("tenders/{tenderId:int}/fail")]
    [RequirePermission(PermissionCode.ConductProcurement)]
    public async Task<IActionResult> Fail(int tenderId, [FromBody] TenderFailRequest request) =>
        await Run(() => _tenders.FailAsync(tenderId, request, _currentUser.UserId));

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new {message = ex.Message});
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }
}
