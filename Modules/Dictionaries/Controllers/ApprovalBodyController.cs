using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Dictionaries.Controllers;

/// <summary>
/// Справочник органов утверждения ВНД (иерархический)
/// </summary>
[ApiController]
[Route("dictionaries/approval-body")]
[Tags("Справочники — Органы утверждения")]
[Authorize]
public class ApprovalBodyController : ControllerBase
{
    private readonly IApprovalBodyService _service;
    private readonly ILanguageResolver _languageResolver;

    public ApprovalBodyController(IApprovalBodyService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>Получить список всех органов утверждения</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ApprovalBodyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ApprovalBodyResponse>>> GetAll(
        [FromQuery] ApprovalBodySortBy sortBy = ApprovalBodySortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>Создать новый орган утверждения</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageDictionaries)]
    [ProducesResponseType(typeof(ApprovalBodyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalBodyResponse>> Create([FromBody] CreateApprovalBodyRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        try
        {
            var result = await _service.CreateAsync(request, language);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Обновить существующий орган утверждения</summary>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageDictionaries)]
    [ProducesResponseType(typeof(ApprovalBodyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalBodyResponse>> Update(int id, [FromBody] UpdateApprovalBodyRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        try
        {
            var result = await _service.UpdateAsync(id, request, language);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>Удалить орган утверждения</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageDictionaries)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}