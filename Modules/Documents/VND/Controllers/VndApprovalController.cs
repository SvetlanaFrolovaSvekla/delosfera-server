using delosfera_server.Common.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

[ApiController]
[Route("/vnd/{vndId:int}/approval")]
[Tags("ВНД — Согласование")]
[Authorize]
public class VndApprovalController : ControllerBase
{
    private readonly IVndApprovalService _service;
    private readonly ICurrentUserService _currentUser;

    public VndApprovalController(IVndApprovalService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Запустить согласование последней редакции ВНД</summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ApprovalProcessResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApprovalProcessResponse>> Start(int vndId, [FromBody] StartApprovalRequest request)
    {
        try
        {
            var result = await _service.StartAsync(vndId, request, _currentUser.UserId);
            return CreatedAtAction(nameof(Get), new { vndId }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Текущий процесс согласования для последней редакции ВНД</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(ApprovalProcessResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalProcessResponse>> Get(int vndId)
    {
        try
        {
            return Ok(await _service.GetByVndIdAsync(vndId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Решение согласующего по своему этапу</summary>
    [HttpPost("stages/{stageId:int}/decision")]
    [ProducesResponseType(typeof(ApprovalProcessResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalProcessResponse>> Decide(
        int vndId, int stageId, [FromBody] ApprovalDecisionRequest request)
    {
        try
        {
            return Ok(await _service.DecideAsync(vndId, stageId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Инициатор отправляет исправленную редакцию на повторное согласование</summary>
    [HttpPost("resubmit")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApprovalProcessResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalProcessResponse>> Resubmit(
        int vndId, [FromForm] ResubmitAfterRevisionRequest request)
    {
        try
        {
            return Ok(await _service.ResubmitAfterRevisionAsync(vndId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }
    
    /// <summary>Добавить строку в матрицу разногласий (только инициатор, только на доработке)</summary>
    [HttpPost("disagreement-matrix/rows")]
    [ProducesResponseType(typeof(DisagreementMatrixRowResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DisagreementMatrixRowResponse>> AddDisagreementRow(
        int vndId, [FromBody] AddDisagreementMatrixRowRequest request)
    {
        try
        {
            return Ok(await _service.AddDisagreementMatrixRowAsync(vndId, request, _currentUser.UserId));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Удалить строку из матрицы разногласий</summary>
    [HttpDelete("disagreement-matrix/rows/{rowId:int}")]
    public async Task<IActionResult> DeleteDisagreementRow(int vndId, int rowId)
    {
        try
        {
            await _service.DeleteDisagreementMatrixRowAsync(vndId, rowId, _currentUser.UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }
}