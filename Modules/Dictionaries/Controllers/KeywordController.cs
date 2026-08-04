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
/// Справочник ключевых слов (иерархический)
/// </summary>
[ApiController]
[Route("dictionaries/keyword")]
[Tags("Справочники — Ключевые слова")]
[Authorize]
public class KeywordController : ControllerBase
{
    private readonly IKeywordService _service;
    private readonly ILanguageResolver _languageResolver;

    public KeywordController(IKeywordService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех ключевых слов
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список ключевых слов получен успешно</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<KeywordResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<KeywordResponse>>> GetAll(
        [FromQuery] KeywordSortBy sortBy = KeywordSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новое ключевое слово
    /// </summary>
    /// <param name="request">Данные нового ключевого слова</param>
    /// <response code="201">Ключевое слово успешно создано</response>
    /// <response code="404">Указанное родительское ключевое слово не найдено</response>
    /// <response code="409">Превышена максимальная глубина вложенности</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageDictionaries)]
    [ProducesResponseType(typeof(KeywordResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KeywordResponse>> Create([FromBody] CreateKeywordRequest request)
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
    /// Обновить существующее ключевое слово
    /// </summary>
    /// <param name="id">Идентификатор ключевого слова</param>
    /// <param name="request">Новые данные ключевого слова</param>
    /// <response code="200">Ключевое слово успешно обновлено</response>
    /// <response code="404">Ключевое слово или родитель не найдены</response>
    /// <response code="409">Циклическая ссылка или превышена глубина вложенности</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageDictionaries)]
    [ProducesResponseType(typeof(KeywordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KeywordResponse>> Update(int id, [FromBody] UpdateKeywordRequest request)
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
    /// Удалить ключевое слово
    /// </summary>
    /// <param name="id">Идентификатор ключевого слова</param>
    /// <response code="204">Ключевое слово успешно удалено</response>
    /// <response code="404">Ключевое слово не найдено</response>
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