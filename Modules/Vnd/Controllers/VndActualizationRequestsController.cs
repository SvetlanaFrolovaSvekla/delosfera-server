using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;
using delosfera_server.Modules.Vnd.Services;

namespace delosfera_server.Modules.Vnd.Controllers;

/// <summary>Заявки на доступ к актуализации — рассмотрение главным редактором ВНД</summary>
[ApiController]
[Route("/vnd/actualization/requests")]
[Tags("ВНД — Актуализация")]
[Authorize]
public class VndActualizationRequestsController : ControllerBase
{
    private readonly IVndActualizationService _service;
    private readonly ICurrentUserService _currentUser;

    public VndActualizationRequestsController(IVndActualizationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Список заявок, ожидающих решения</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VndActualizationRequestResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndActualizationRequestResponse>>> GetPending()
    {
        try
        {
            return Ok(await _service.GetPendingRequestsAsync(_currentUser.UserId));
        }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Одобрить или отклонить заявку</summary>
    [HttpPost("{requestId:int}/decision")]
    [ProducesResponseType(typeof(VndActualizationRequestResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndActualizationRequestResponse>> Decide(
        int requestId, [FromBody] ActualizationRequestDecisionRequest request)
    {
        try
        {
            return Ok(await _service.DecideRequestAsync(requestId, request.Approve, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }
}