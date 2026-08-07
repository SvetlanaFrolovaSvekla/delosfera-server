using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Справочник обязательных участников процесса согласования ВНД.
/// Набор из 4 фиксированных этапов (Юр. управление, Риск-менеджмент, Комплаенс, Методология) -
/// создавать/удалять записи нельзя, только менять согласующего по умолчанию.
/// </summary>
[ApiController]
[Route("dictionaries/coordination-users")]
[Tags("Справочники — Обязательные участники согласования")]
[Authorize]
public class CoordinationDefaultApproverController : ControllerBase
{
    private readonly ICoordinationDefaultApproverService _service;

    public CoordinationDefaultApproverController(ICoordinationDefaultApproverService service)
    {
        _service = service;
    }

    /// <summary>Получить дефолтных согласующих по всем фиксированным этапам</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CoordinationDefaultApproverResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CoordinationDefaultApproverResponse>>> GetAll() =>
        Ok(await _service.GetAllAsync());

    /// <summary>Изменить согласующего по умолчанию для одного из этапов</summary>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(CoordinationDefaultApproverResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CoordinationDefaultApproverResponse>> Update(
        int id, [FromBody] UpdateCoordinationDefaultApproverRequest request)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, request));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}