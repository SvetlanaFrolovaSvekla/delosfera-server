using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Services;

namespace delosfera_server.Modules.Vnd.Controllers;

[ApiController]
[Route("/vnd/{vndId:int}/actualization")]
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

    /// <summary>Подтвердить старт актуализации после одобренной заявки (только сдвиг периода)</summary>
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
}