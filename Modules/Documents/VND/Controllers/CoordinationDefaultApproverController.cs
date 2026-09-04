using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Справочник обязательных (фиксированных) этапов процесса согласования ВНД. Полноценный CRUD:
/// можно менять название этапа, СП и согласующего по умолчанию, а также добавлять и удалять
/// записи - все активные записи обязательны в маршруте согласования, в порядке Order.
/// </summary>
[ApiController]
[Route("api/dictionaries/coordination-users")]
[Tags("Справочники — Обязательные участники согласования")]
[Authorize]
public class CoordinationDefaultApproverController : ControllerBase
{
    private readonly ICoordinationDefaultApproverService _service;

    public CoordinationDefaultApproverController(ICoordinationDefaultApproverService service)
    {
        _service = service;
    }

    /// <summary>Получить все фиксированные этапы (в порядке маршрута)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CoordinationDefaultApproverResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CoordinationDefaultApproverResponse>>> GetAll() =>
        Ok(await _service.GetAllAsync());

    /// <summary>Добавить новый обязательный этап (добавляется последним в маршруте)</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(CoordinationDefaultApproverResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CoordinationDefaultApproverResponse>> Create(
        [FromBody] CreateCoordinationDefaultApproverRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Изменить название, СП и/или согласующего по умолчанию одного из этапов</summary>
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

    /// <summary>Удалить обязательный этап из маршрута согласования</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Изменить порядок этапов в маршруте (перетаскивание в справочнике)</summary>
    [HttpPost("reorder")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(List<CoordinationDefaultApproverResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<List<CoordinationDefaultApproverResponse>>> Reorder(
        [FromBody] ReorderCoordinationDefaultApproverRequest request)
    {
        try
        {
            return Ok(await _service.ReorderAsync(request));
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
