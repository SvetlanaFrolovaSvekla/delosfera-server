using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Предложения по ВНД: отправить может любой, кому виден ВНД (кнопка "+ Предложения по ВНД"
/// на странице документа), читать и отмечать прочитанными — только получатели с правом
/// <see cref="PermissionCode.ManageVndProposals"/> (главный редактор ВНД, страница
/// "Нормотворчество (ВНД)" → "Предложения по ВНД").
/// </summary>
[ApiController]
[Authorize]
[Tags("ВНД — Предложения")]
public class VndProposalsController : ControllerBase
{
    private readonly IVndProposalService _service;
    private readonly ICurrentUserService _currentUser;

    public VndProposalsController(IVndProposalService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Отправить предложение по ВНД (multipart/form-data: Text, RedactionId, QuotesJson, Files)</summary>
    [HttpPost("/api/vnd/{vndId:int}/proposals")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VndProposalCreatedResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndProposalCreatedResponse>> Create(
        int vndId, [FromForm] CreateVndProposalRequest request, CancellationToken ct)
    {
        // Файлы - напрямую из Request.Form.Files (см. VndApprovalController.Decide: биндинг
        // List<IFormFile> через комплексный [FromForm]-объект ненадёжен).
        var files = Request.Form.Files
            .Where(f => f.Name == nameof(CreateVndProposalRequest.Files))
            .ToList();

        try
        {
            return Ok(await _service.CreateAsync(vndId, request, files, _currentUser.UserId, ct));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Список предложений (сначала непрочитанные)</summary>
    [HttpGet("/api/vnd-proposals")]
    [RequirePermission(PermissionCode.ManageVndProposals)]
    [ProducesResponseType(typeof(VndProposalPagedResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndProposalPagedResponse>> Search(
        [FromQuery] VndProposalFilterRequest filter, CancellationToken ct)
    {
        return Ok(await _service.SearchAsync(filter, ct));
    }

    /// <summary>Счётчики: всего / непрочитанных — для бейджа в меню и вкладок страницы</summary>
    [HttpGet("/api/vnd-proposals/counts")]
    [RequirePermission(PermissionCode.ManageVndProposals)]
    [ProducesResponseType(typeof(VndProposalCountsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndProposalCountsResponse>> Counts(CancellationToken ct)
    {
        return Ok(await _service.GetCountsAsync(ct));
    }

    /// <summary>Одно предложение (переход из уведомления)</summary>
    [HttpGet("/api/vnd-proposals/{id:int}")]
    [RequirePermission(PermissionCode.ManageVndProposals)]
    [ProducesResponseType(typeof(VndProposalResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndProposalResponse>> Get(int id, CancellationToken ct)
    {
        try { return Ok(await _service.GetByIdAsync(id, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Отметить прочитанным</summary>
    [HttpPost("/api/vnd-proposals/{id:int}/read")]
    [RequirePermission(PermissionCode.ManageVndProposals)]
    [ProducesResponseType(typeof(VndProposalResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndProposalResponse>> MarkAsRead(int id, CancellationToken ct)
    {
        try { return Ok(await _service.MarkAsReadAsync(id, _currentUser.UserId, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Вернуть в непрочитанные</summary>
    [HttpPost("/api/vnd-proposals/{id:int}/unread")]
    [RequirePermission(PermissionCode.ManageVndProposals)]
    [ProducesResponseType(typeof(VndProposalResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndProposalResponse>> MarkAsUnread(int id, CancellationToken ct)
    {
        try { return Ok(await _service.MarkAsUnreadAsync(id, ct)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Отметить все прочитанными</summary>
    [HttpPost("/api/vnd-proposals/read-all")]
    [RequirePermission(PermissionCode.ManageVndProposals)]
    public async Task<ActionResult<object>> MarkAllAsRead(CancellationToken ct)
    {
        var updated = await _service.MarkAllAsReadAsync(_currentUser.UserId, ct);
        return Ok(new { updated });
    }
}
