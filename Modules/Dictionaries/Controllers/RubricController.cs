using delosfera_server.Common.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;

namespace delosfera_server.Modules.Dictionaries.Controllers;

/// <summary>
/// Справочник рубрик (иерархический)
/// </summary>
[ApiController]
[Route("dictionaries/rubric")]
[Tags("Справочники — Рубрикатор")]
[Authorize]
public class RubricController : ControllerBase
{
    private readonly IRubricService _service;
    private readonly ILanguageResolver _languageResolver;

    public RubricController(IRubricService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех рубрик
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список рубрик получен успешно</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<RubricResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RubricResponse>>> GetAll(
        [FromQuery] RubricSortBy sortBy = RubricSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новую рубрику
    /// </summary>
    /// <param name="request">Данные новой рубрики</param>
    /// <response code="201">Рубрика успешно создана</response>
    /// <response code="404">Указанная родительская рубрика не найдена</response>
    /// <response code="409">Превышена максимальная глубина вложенности</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageDictionaries)]
    [ProducesResponseType(typeof(RubricResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RubricResponse>> Create([FromBody] CreateRubricRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _service.CreateAsync(request, language);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Обновить существующую рубрику
    /// </summary>
    /// <param name="id">Идентификатор рубрики</param>
    /// <param name="request">Новые данные рубрики</param>
    /// <response code="200">Рубрика успешно обновлена</response>
    /// <response code="404">Рубрика или родитель не найдены</response>
    /// <response code="409">Циклическая ссылка или превышена глубина вложенности</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageDictionaries)]
    [ProducesResponseType(typeof(RubricResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RubricResponse>> Update(int id, [FromBody] UpdateRubricRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _service.UpdateAsync(id, request, language);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Удалить рубрику
    /// </summary>
    /// <param name="id">Идентификатор рубрики</param>
    /// <response code="204">Рубрика успешно удалена</response>
    /// <response code="404">Рубрика не найдена</response>
    /// <response code="409">Есть дочерние записи или ссылки в других документах</response>
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}