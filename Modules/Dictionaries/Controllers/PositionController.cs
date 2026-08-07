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
/// Справочник должностей сотрудников банка
/// </summary>
[ApiController]
[Route("dictionaries/position")]
[Tags("Справочники — Должности")]
[Authorize]
public class PositionController : ControllerBase
{
    private readonly IPositionService _service;
    private readonly ILanguageResolver _languageResolver;

    public PositionController(IPositionService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех должностей
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список должностей получен успешно</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<PositionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PositionResponse>>> GetAll(
        [FromQuery] PositionSortBy sortBy = PositionSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новую должность
    /// </summary>
    /// <param name="request">Данные новой должности</param>
    /// <response code="201">Должность успешно создана</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PositionResponse>> Create([FromBody] CreatePositionRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.CreateAsync(request, language);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    /// <summary>
    /// Обновить существующую должность
    /// </summary>
    /// <param name="id">Идентификатор должности</param>
    /// <param name="request">Новые данные должности</param>
    /// <response code="200">Должность успешно обновлена</response>
    /// <response code="404">Должность с указанным id не найдена</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionResponse>> Update(int id, [FromBody] UpdatePositionRequest request)
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
    }

    /// <summary>
    /// Удалить должность
    /// </summary>
    /// <param name="id">Идентификатор должности</param>
    /// <response code="204">Должность успешно удалена</response>
    /// <response code="404">Должность с указанным id не найдена</response>
    /// <response code="409">Нельзя удалить - на должность есть ссылки в других документах</response>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageGeneralDictionaries)]
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