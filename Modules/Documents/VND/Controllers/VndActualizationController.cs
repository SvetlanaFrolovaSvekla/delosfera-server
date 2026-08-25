using delosfera_server.Common.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

[ApiController]
[Route("/api/vnd/{vndId:int}/actualization")]
[Tags("ВНД — Актуализация")]
[Authorize]
public class VndActualizationController : ControllerBase
{
    private readonly IVndActualizationService _service;
    private readonly ICurrentUserService _currentUser;

    public VndActualizationController(IVndActualizationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Сразу начать актуализацию (для ActualizeAnyVndWithApproval/WithoutApproval)</summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(VndActualizationStateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndActualizationStateResponse>> Start(
        int vndId, [FromBody] StartActualizationRequest request)
    {
        try
        {
            return Ok(await _service.StartAsync(vndId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Запросить доступ к актуализации у главного редактора (для ActualizeVnd...ByRequest)</summary>
    [HttpPost("request-access")]
    [ProducesResponseType(typeof(VndActualizationRequestResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<VndActualizationRequestResponse>> RequestAccess(
        int vndId, [FromBody] RequestActualizationAccessRequest request)
    {
        try
        {
            var result = await _service.RequestAccessAsync(vndId, request, _currentUser.UserId);
            return CreatedAtAction(
                nameof(VndActualizationRequestsController.GetPending),
                "VndActualizationRequests", null, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Выполнить актуализацию (шаг Б для цикла, начатого напрямую через /start) —
    /// зафиксировать финальные сдвиг срока/"без изменений". Доступно ответственному за
    /// актуализацию или главному редактору ВНД</summary>
    [HttpPost("perform")]
    [ProducesResponseType(typeof(VndActualizationStateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndActualizationStateResponse>> Perform(
        int vndId, [FromBody] PerformActualizationRequest request)
    {
        try
        {
            return Ok(await _service.PerformAsync(vndId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Подтвердить старт актуализации после одобренной заявки — совмещает старт цикла
    /// и шаг "Выполнить актуализацию" (для пути "по заявке")</summary>
    [HttpPost("confirm-start")]
    [ProducesResponseType(typeof(VndActualizationStateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndActualizationStateResponse>> ConfirmStart(
        int vndId, [FromBody] ConfirmActualizationStartRequest request)
    {
        try
        {
            return Ok(await _service.ConfirmStartAfterRequestAsync(vndId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Подтвердить, что заявленная "актуализация без изменений" прошла без изменений
    /// (только когда согласование для цикла не требуется) — OnActualization → Consolidation
    /// напрямую, без загрузки новой редакции</summary>
    [HttpPost("confirm-no-changes")]
    [ProducesResponseType(typeof(VndActualizationStateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndActualizationStateResponse>> ConfirmNoChanges(int vndId)
    {
        try
        {
            return Ok(await _service.ConfirmNoChangesAsync(vndId, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Опубликовать новую редакцию после консолидации (Consolidation → Active)</summary>
    [HttpPost("publish")]
    [ProducesResponseType(typeof(VndActualizationStateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndActualizationStateResponse>> Publish(
        int vndId, [FromBody] PublishVndActualizationRequest request)
    {
        try
        {
            return Ok(await _service.PublishAsync(vndId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>История циклов актуализации документа — кто и когда актуализировал,
    /// от самого нового к самому старому</summary>
    [HttpGet("history")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(List<VndActualizationRecordResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndActualizationRecordResponse>>> GetHistory(int vndId)
    {
        try
        {
            return Ok(await _service.GetHistoryAsync(vndId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Все заявки на доступ к актуализации этого документа (любого статуса) — кто
    /// когда запросил доступ и кто его выдал/отклонил. Доступно всем, кто может просматривать
    /// ВНД (не только главному редактору — в отличие от /vnd/actualization/requests)</summary>
    [HttpGet("requests")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(List<VndActualizationRequestResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndActualizationRequestResponse>>> GetRequests(int vndId)
    {
        try
        {
            return Ok(await _service.GetRequestHistoryAsync(vndId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}