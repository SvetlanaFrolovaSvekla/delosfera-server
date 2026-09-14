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
/// Справочник рубрик служебных записок (иерархический) — отдельный от Рубрикатора ВНД
/// (RubricController), ведётся и используется независимо
/// </summary>
[ApiController]
[Route("api/dictionaries/sz-rubric")]
[Tags("Справочники — Рубрикатор СЗ")]
[Authorize]
public class SzRubricController : ControllerBase
{
    private readonly ISzRubricService _service;
    private readonly ILanguageResolver _languageResolver;

    public SzRubricController(ISzRubricService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех рубрик СЗ
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список рубрик получен успешно</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<SzRubricResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SzRubricResponse>>> GetAll(
        [FromQuery] SzRubricSortBy sortBy = SzRubricSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новую рубрику СЗ
    /// </summary>
    /// <param name="request">Данные новой рубрики</param>
    /// <response code="201">Рубрика успешно создана</response>
    /// <response code="404">Указанная родительская рубрика не найдена</response>
    /// <response code="409">Превышена максимальная глубина вложенности</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageSzDictionaries)]
    [ProducesResponseType(typeof(SzRubricResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SzRubricResponse>> Create([FromBody] CreateSzRubricRequest request)
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
    /// Обновить существующую рубрику СЗ
    /// </summary>
    /// <param name="id">Идентификатор рубрики</param>
    /// <param name="request">Новые данные рубрики</param>
    /// <response code="200">Рубрика успешно обновлена</response>
    /// <response code="404">Рубрика или родитель не найдены</response>
    /// <response code="409">Циклическая ссылка или превышена глубина вложенности</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageSzDictionaries)]
    [ProducesResponseType(typeof(SzRubricResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SzRubricResponse>> Update(int id, [FromBody] UpdateSzRubricRequest request)
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
    /// Удалить рубрику СЗ
    /// </summary>
    /// <param name="id">Идентификатор рубрики</param>
    /// <response code="204">Рубрика успешно удалена</response>
    /// <response code="404">Рубрика не найдена</response>
    /// <response code="409">Есть дочерние записи или ссылки в других документах</response>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageSzDictionaries)]
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
