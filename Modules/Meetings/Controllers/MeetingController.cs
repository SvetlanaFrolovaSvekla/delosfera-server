using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Meetings.Controllers;

/// <summary>Журнал заседаний Правления, КПА и комитетов.</summary>
[ApiController]
[Route("api/meetings")]
[Tags("Заседания")]
[Authorize]
public class MeetingController : MeetingControllerBase
{
    private readonly IMeetingService _meetings;
    private readonly IMeetingNotificationService _notifications;
    private readonly IMeetingRegistryService _registry;
    private readonly ICurrentUserService _currentUser;

    public MeetingController(
        IMeetingService meetings,
        IMeetingNotificationService notifications,
        IMeetingRegistryService registry,
        ICurrentUserService currentUser)
    {
        _meetings = meetings;
        _notifications = notifications;
        _registry = registry;
        _currentUser = currentUser;
    }

    /// <summary>Журнал заседаний. Показывает только то, что доступно текущему пользователю.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] MeetingBody? body,
        [FromQuery] int? year,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool overdueOnly = false) =>
        await Run(() => _meetings.ListAsync(new MeetingFilterRequest
        {
            Body = body, Year = year, From = from, To = to, OverdueOnly = overdueOnly,
        }));

    /// <summary>Карточка заседания с повесткой.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) => await Run(() => _meetings.GetAsync(id));

    /// <summary>Завести заседание.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MeetingCreateRequest request) =>
        await Run(() => _meetings.CreateAsync(request, _currentUser.UserId));

    /// <summary>Изменить реквизиты заседания.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] MeetingUpdateRequest request) =>
        await Run(() => _meetings.UpdateAsync(id, request));

    /// <summary>Удалить заседание без повестки.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        await Run(async () => { await _meetings.DeleteAsync(id); return true; });

    /// <summary>Отправить уведомление о заседании участникам.</summary>
    [HttpPost("{id:int}/notify")]
    public async Task<IActionResult> Notify(int id) =>
        await Run(() => _notifications.NotifyAboutMeetingAsync(id, _currentUser.UserId));

    /// <summary>Реестр решений за период в Excel.</summary>
    [HttpGet("registry")]
    public async Task<IActionResult> Registry(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] MeetingBody? body)
    {
        if (!_currentUser.HasPermission(Users.Models.PermissionCode.ExportMeetingRegistry))
            return StatusCode(403, new { message = "Выгрузка реестра доступна секретарям" });

        try
        {
            var bytes = await _registry.ExportAsync(from, to, body);
            var name = $"Реестр решений {from:dd.MM.yyyy}-{to:dd.MM.yyyy}.xlsx";

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                name);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
