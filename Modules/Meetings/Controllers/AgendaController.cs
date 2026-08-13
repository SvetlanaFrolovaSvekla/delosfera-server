using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Meetings.Controllers;

/// <summary>Повестка дня заседания: вопросы, приглашённые, поручения и отчёты.</summary>
[ApiController]
[Route("api/meetings")]
[Tags("Заседания — повестка")]
[Authorize]
public class AgendaController : MeetingControllerBase
{
    private readonly IAgendaService _agenda;
    private readonly IAgendaFileService _files;
    private readonly ICurrentUserService _currentUser;

    public AgendaController(
        IAgendaService agenda,
        IAgendaFileService files,
        ICurrentUserService currentUser)
    {
        _agenda = agenda;
        _files = files;
        _currentUser = currentUser;
    }

    /// <summary>Добавить вопрос в повестку.</summary>
    [HttpPost("{meetingId:int}/items")]
    public async Task<IActionResult> AddItem(int meetingId, [FromBody] AgendaItemRequest request) =>
        await Run(() => _agenda.AddItemAsync(meetingId, request));

    /// <summary>Изменить вопрос повестки.</summary>
    [HttpPut("items/{itemId:int}")]
    public async Task<IActionResult> UpdateItem(int itemId, [FromBody] AgendaItemRequest request) =>
        await Run(() => _agenda.UpdateItemAsync(itemId, request));

    /// <summary>Удалить вопрос, по которому ещё нет отчётов.</summary>
    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> DeleteItem(int itemId) =>
        await Run(async () => { await _agenda.DeleteItemAsync(itemId); return true; });

    /// <summary>Пригласить сотрудника на рассмотрение вопроса.</summary>
    [HttpPost("items/{itemId:int}/guests")]
    public async Task<IActionResult> AddGuest(int itemId, [FromBody] AgendaGuestRequest request) =>
        await Run(() => _agenda.AddGuestAsync(itemId, request));

    /// <summary>Убрать приглашённого.</summary>
    [HttpDelete("guests/{guestId:int}")]
    public async Task<IActionResult> RemoveGuest(int guestId) =>
        await Run(() => _agenda.RemoveGuestAsync(guestId));

    /// <summary>Назначить ответственного со сроком исполнения.</summary>
    [HttpPost("items/{itemId:int}/assignments")]
    public async Task<IActionResult> AddAssignment(int itemId, [FromBody] AgendaAssignmentRequest request) =>
        await Run(() => _agenda.AddAssignmentAsync(itemId, request));

    /// <summary>Снять поручение, по которому нет отчёта.</summary>
    [HttpDelete("assignments/{assignmentId:int}")]
    public async Task<IActionResult> RemoveAssignment(int assignmentId) =>
        await Run(() => _agenda.RemoveAssignmentAsync(assignmentId));

    /// <summary>Заполнить отчёт об исполнении и статус поручения.</summary>
    [HttpPost("assignments/{assignmentId:int}/report")]
    public async Task<IActionResult> Report(int assignmentId, [FromBody] AgendaReportRequest request) =>
        await Run(() => _agenda.ReportAsync(assignmentId, request, _currentUser.UserId));

    /// <summary>Приложить файл к вопросу: служебную записку, протокол или документ об исполнении.</summary>
    [HttpPost("items/{itemId:int}/files")]
    public async Task<IActionResult> Attach(
        int itemId, [FromQuery] MeetingFileKind kind, IFormFile file) =>
        await Run(() => _files.AttachAsync(itemId, kind, file, _currentUser.UserId));

    /// <summary>Удалить приложенный файл (только секретарь).</summary>
    [HttpDelete("files/{agendaFileId:int}")]
    public async Task<IActionResult> Detach(int agendaFileId) =>
        await Run(async () => { await _files.DetachAsync(agendaFileId); return true; });

    /// <summary>Скачать приложенный файл.</summary>
    [HttpGet("files/{agendaFileId:int}")]
    public async Task<IActionResult> Download(int agendaFileId)
    {
        try
        {
            var (stream, contentType, fileName) = await _files.DownloadAsync(agendaFileId);
            return File(stream, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }
}
