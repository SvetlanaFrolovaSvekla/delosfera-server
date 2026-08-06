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
/// Справочник уровней секретности документов
/// </summary>
[ApiController]
[Route("dictionaries/security-level")]
[Tags("Справочники — Уровни секретности")]
[Authorize]
public class SecurityLevelController : ControllerBase
{
    private readonly ISecurityLevelService _service;
    private readonly ILanguageResolver _languageResolver;

    public SecurityLevelController(ISecurityLevelService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<SecurityLevelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SecurityLevelResponse>>> GetAll(
        [FromQuery] SecurityLevelSortBy sortBy = SecurityLevelSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новый уровень секретности
    /// </summary>
    /// <param name="request">Данные нового уровня секретности</param>
    /// <response code="201">Уровень секретности успешно создан</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(SecurityLevelResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<SecurityLevelResponse>> Create([FromBody] CreateSecurityLevelRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.CreateAsync(request, language);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    /// <summary>
    /// Обновить существующий уровень секретности
    /// </summary>
    /// <param name="id">Идентификатор уровня секретности</param>
    /// <param name="request">Новые данные уровня секретности</param>
    /// <response code="200">Уровень секретности успешно обновлён</response>
    /// <response code="404">Уровень секретности с указанным id не найден</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(SecurityLevelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecurityLevelResponse>> Update(int id, [FromBody] UpdateSecurityLevelRequest request)
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
    /// Удалить уровень секретности
    /// </summary>
    /// <param name="id">Идентификатор уровня секретности</param>
    /// <response code="204">Уровень секретности успешно удалён</response>
    /// <response code="404">Уровень секретности с указанным id не найден</response>
    /// <response code="409">Нельзя удалить — на уровень секретности есть ссылки в других документах</response>
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