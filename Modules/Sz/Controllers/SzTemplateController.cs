using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Services;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>
/// Личные шаблоны служебных записок (СЗ-6): заготовки полей формы, которые автор
/// применяет при создании новой записки.
/// </summary>
[ApiController]
[Route("api/sz/templates")]
[Tags("СЗ — Шаблоны записок")]
[Authorize]
public class SzTemplateController : ControllerBase
{
    private readonly ISzTemplateService _templates;
    private readonly ICurrentUserService _currentUser;

    public SzTemplateController(ISzTemplateService templates, ICurrentUserService currentUser)
    {
        _templates = templates;
        _currentUser = currentUser;
    }

    /// <summary>Мои шаблоны записок.</summary>
    [HttpGet]
    public async Task<ActionResult<List<SzTemplateResponse>>> List() =>
        Ok(await _templates.ListAsync(_currentUser.UserId));

    /// <summary>Сохранить текущую форму как шаблон.</summary>
    [HttpPost]
    public async Task<ActionResult<SzTemplateResponse>> Create([FromBody] SzTemplateSaveRequest request)
    {
        try { return Ok(await _templates.CreateAsync(request, _currentUser.UserId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Изменить шаблон.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<SzTemplateResponse>> Update(int id, [FromBody] SzTemplateSaveRequest request)
    {
        try { return Ok(await _templates.UpdateAsync(id, request, _currentUser.UserId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Удалить шаблон.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _templates.DeleteAsync(id, _currentUser.UserId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
