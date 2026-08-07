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
/// Справочник видов внутренних нормативных документов (ВНД)
/// </summary>
[ApiController]
[Route("dictionaries/type-vnd")]
[Tags("Справочники — Виды ВНД")]
[Authorize]
public class TypeVndController : ControllerBase
{
    private readonly ITypeVndService _service;
    private readonly ILanguageResolver _languageResolver;

    public TypeVndController(ITypeVndService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех видов ВНД
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список видов ВНД получен успешно</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<TypeVndResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TypeVndResponse>>> GetAll(
        [FromQuery] TypeVndSortBy sortBy = TypeVndSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новый вид ВНД
    /// </summary>
    /// <param name="request">Данные нового вида ВНД</param>
    /// <response code="201">Вид ВНД успешно создан</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(TypeVndResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<TypeVndResponse>> Create([FromBody] CreateTypeVndRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.CreateAsync(request, language);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    /// <summary>
    /// Обновить существующий вид ВНД
    /// </summary>
    /// <param name="id">Идентификатор вида ВНД</param>
    /// <param name="request">Новые данные вида ВНД</param>
    /// <response code="200">Вид ВНД успешно обновлён</response>
    /// <response code="404">Вид ВНД с указанным id не найден</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(TypeVndResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TypeVndResponse>> Update(int id, [FromBody] UpdateTypeVndRequest request)
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
    /// Удалить вид ВНД
    /// </summary>
    /// <param name="id">Идентификатор вида ВНД</param>
    /// <response code="204">Вид ВНД успешно удалён</response>
    /// <response code="404">Вид ВНД с указанным id не найден</response>
    /// <response code="409">Нельзя удалить - на вид ВНД есть ссылки в других документах</response>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
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