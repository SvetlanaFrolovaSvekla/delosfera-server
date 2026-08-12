using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Search.DTO;
using delosfera_server.Modules.Search.Services;

namespace delosfera_server.Modules.Search.Controllers;

/// <summary>Поиск по документам и сохранённые фильтры (GEN-02, GEN-04).</summary>
[ApiController]
[Route("api/search")]
[Tags("Поиск")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _search;
    private readonly ISavedSearchService _saved;
    private readonly ICurrentUserService _currentUser;

    public SearchController(
        ISearchService search,
        ISavedSearchService saved,
        ICurrentUserService currentUser)
    {
        _search = search;
        _saved = saved;
        _currentUser = currentUser;
    }

    /// <summary>Поиск по реквизитам и текстовым полям карточек.</summary>
    [HttpPost]
    public async Task<IActionResult> Search([FromBody] SearchRequest request) =>
        Ok(await _search.SearchAsync(request, _currentUser.UserId));

    /// <summary>Сохранённые фильтры текущего сотрудника.</summary>
    [HttpGet("saved")]
    public async Task<IActionResult> Saved() =>
        Ok(await _saved.ListAsync(_currentUser.UserId));

    /// <summary>Сохранить фильтр под названием; повтор названия обновляет условия.</summary>
    [HttpPost("saved")]
    public async Task<IActionResult> Save([FromBody] SaveSearchRequest request)
    {
        try
        {
            return Ok(await _saved.SaveAsync(request, _currentUser.UserId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new {message = ex.Message});
        }
    }

    /// <summary>Удалить сохранённый фильтр.</summary>
    [HttpDelete("saved/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _saved.DeleteAsync(id, _currentUser.UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new {message = ex.Message});
        }
    }
}
