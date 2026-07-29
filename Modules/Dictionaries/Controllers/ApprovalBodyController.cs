using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;
using delosfera_server.Modules.Dictionaries.Services;

namespace delosfera_server.Modules.Dictionaries.Controllers;

/// <summary>
/// Справочник органов утверждения ВНД (иерархический)
/// </summary>
[ApiController]
[Route("api/dictionaries/approval-body")]
[Tags("Справочники — Органы утверждения")]
public class ApprovalBodyController : ControllerBase
{
    private readonly IApprovalBodyService _service;
    private readonly ILanguageResolver _languageResolver;

    public ApprovalBodyController(IApprovalBodyService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех органов утверждения
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список органов утверждения получен успешно</response>
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

    /// <summary>
    /// Создать новый орган утверждения
    /// </summary>
    /// <param name="request">Данные нового органа утверждения</param>
    /// <response code="201">Орган утверждения успешно создан</response>
    /// <response code="404">Указанный родительский орган не найден</response>
    /// <response code="409">Превышена максимальная глубина вложенности</response>
    [HttpPost]
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
    /// Обновить существующий орган утверждения
    /// </summary>
    /// <param name="id">Идентификатор органа утверждения</param>
    /// <param name="request">Новые данные органа утверждения</param>
    /// <response code="200">Орган утверждения успешно обновлён</response>
    /// <response code="404">Орган утверждения или родитель не найден</response>
    /// <response code="409">Циклическая ссылка или превышена глубина вложенности</response>
    [HttpPut("{id:int}")]
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
    /// Удалить орган утверждения
    /// </summary>
    /// <param name="id">Идентификатор органа утверждения</param>
    /// <response code="204">Орган утверждения успешно удалён</response>
    /// <response code="404">Орган утверждения не найден</response>
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