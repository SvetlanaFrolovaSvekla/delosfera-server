using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Users.Services;

namespace delosfera_server.Modules.Users.Controllers;

/// <summary>Личные сохранённые фильтры реестров (БП-16).</summary>
[ApiController]
[Route("api/saved-filters")]
[Tags("Сохранённые фильтры")]
[Authorize]
public class SavedFilterController : ControllerBase
{
    private readonly ISavedFilterService _filters;
    private readonly ICurrentUserService _currentUser;

    public SavedFilterController(ISavedFilterService filters, ICurrentUserService currentUser)
    {
        _filters = filters;
        _currentUser = currentUser;
    }

    /// <summary>Мои фильтры; scope сужает до одного реестра (sz | procurement).</summary>
    [HttpGet]
    public async Task<ActionResult<List<SavedFilterResponse>>> List([FromQuery] string? scope) =>
        Ok(await _filters.ListAsync(_currentUser.UserId, scope));

    /// <summary>Сохранить текущий фильтр.</summary>
    [HttpPost]
    public async Task<ActionResult<SavedFilterResponse>> Create([FromBody] SavedFilterSaveRequest request)
    {
        try { return Ok(await _filters.CreateAsync(request, _currentUser.UserId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Удалить фильтр.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _filters.DeleteAsync(id, _currentUser.UserId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
