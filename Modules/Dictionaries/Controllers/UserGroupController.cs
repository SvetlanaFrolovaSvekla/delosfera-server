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
/// Справочник групп пользователей
/// </summary>
[ApiController]
[Route("dictionaries/user-group")]
[Tags("Справочники — Группы пользователей")]
[Authorize]
public class UserGroupController : ControllerBase
{
    private readonly IUserGroupService _service;
    private readonly ILanguageResolver _languageResolver;

    public UserGroupController(IUserGroupService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех групп пользователей
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список групп получен успешно</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserGroupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserGroupResponse>>> GetAll(
        [FromQuery] UserGroupSortBy sortBy = UserGroupSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новую группу пользователей
    /// </summary>
    /// <param name="request">Данные новой группы</param>
    /// <response code="201">Группа успешно создана</response>
    /// <response code="404">Один из указанных пользователей не найден</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(UserGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserGroupResponse>> Create([FromBody] CreateUserGroupRequest request)
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
    }

    /// <summary>
    /// Обновить существующую группу пользователей
    /// </summary>
    /// <param name="id">Идентификатор группы</param>
    /// <param name="request">Новые данные группы</param>
    /// <response code="200">Группа успешно обновлена</response>
    /// <response code="404">Группа или один из пользователей не найдены</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageVndDictionaries)]
    [ProducesResponseType(typeof(UserGroupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserGroupResponse>> Update(int id, [FromBody] UpdateUserGroupRequest request)
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
    /// Удалить группу пользователей
    /// </summary>
    /// <param name="id">Идентификатор группы</param>
    /// <response code="204">Группа успешно удалена</response>
    /// <response code="404">Группа не найдена</response>
    /// <response code="409">Нельзя удалить — на группу есть ссылки в других данных</response>
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