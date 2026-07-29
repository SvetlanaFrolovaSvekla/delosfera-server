using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Services;

namespace delosfera_server.Modules.Dictionaries.Controllers;

/// <summary>
/// Справочник структурных подразделений банка (иерархический)
/// </summary>
[ApiController]
[Route("api/dictionaries/organization-unit")]
[Tags("Справочники — Структурные подразделения")]
public class OrganizationUnitController : ControllerBase
{
    private readonly IOrganizationUnitService _service;
    private readonly ILanguageResolver _languageResolver;

    public OrganizationUnitController(IOrganizationUnitService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех структурных подразделений
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список подразделений получен успешно</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<OrganizationUnitResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrganizationUnitResponse>>> GetAll(
        [FromQuery] OrganizationUnitSortBy sortBy = OrganizationUnitSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новое структурное подразделение
    /// </summary>
    /// <param name="request">Данные нового подразделения</param>
    /// <response code="201">Подразделение успешно создано</response>
    /// <response code="404">Указанное родительское подразделение не найдено</response>
    /// <response code="409">Превышена максимальная глубина вложенности</response>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationUnitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationUnitResponse>> Create([FromBody] CreateOrganizationUnitRequest request)
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
    /// Обновить существующее структурное подразделение
    /// </summary>
    /// <param name="id">Идентификатор подразделения</param>
    /// <param name="request">Новые данные подразделения</param>
    /// <response code="200">Подразделение успешно обновлено</response>
    /// <response code="404">Подразделение или родитель не найдены</response>
    /// <response code="409">Циклическая ссылка или превышена глубина вложенности</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OrganizationUnitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationUnitResponse>> Update(int id, [FromBody] UpdateOrganizationUnitRequest request)
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
    /// Удалить структурное подразделение
    /// </summary>
    /// <param name="id">Идентификатор подразделения</param>
    /// <response code="204">Подразделение успешно удалено</response>
    /// <response code="404">Подразделение не найдено</response>
    /// <response code="409">Есть дочерние записи или ссылки в других документах</response>
    [HttpDelete("{id:int}")]
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