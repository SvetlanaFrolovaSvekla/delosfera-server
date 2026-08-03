using delosfera_server.Common.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;

namespace delosfera_server.Modules.Users.Controllers;

/// <summary>
/// Управление ролями и правами доступа
/// </summary>
[ApiController]
[Route("api/users/roles")]
[Tags("Пользователи — Роли")]
public class RoleController : ControllerBase
{
    private readonly IRoleService _service;
    private readonly ILanguageResolver _languageResolver;

    public RoleController(IRoleService service, ILanguageResolver languageResolver)
    {
        _service = service;
        _languageResolver = languageResolver;
    }

    /// <summary>
    /// Получить список всех доступных прав (для построения формы создания/редактирования роли)
    /// </summary>
    /// <response code="200">Список прав получен успешно</response>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(List<PermissionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PermissionResponse>>> GetAllPermissions()
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllPermissionsAsync(language);
        return Ok(result);
    }

    /// <summary>
    /// Получить список всех ролей
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по названию на любом из трёх языков (регистронезависимый)</param>
    /// <response code="200">Список ролей получен успешно</response>
    [HttpGet]
    [RequirePermission(PermissionCode.ManageRoles)]
    [ProducesResponseType(typeof(List<RoleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RoleResponse>>> GetAll(
        [FromQuery] RoleSortBy sortBy = RoleSortBy.CreatedAtAsc,
        [FromQuery] string? search = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, language);
        return Ok(result);
    }

    /// <summary>
    /// Создать новую роль
    /// </summary>
    /// <param name="request">Данные новой роли</param>
    /// <response code="201">Роль успешно создана</response>
    /// <response code="409">В списке прав указан неизвестный код</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageRoles)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleResponse>> Create([FromBody] CreateRoleRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _service.CreateAsync(request, language);
            return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Обновить существующую роль
    /// </summary>
    /// <param name="id">Идентификатор роли</param>
    /// <param name="request">Новые данные роли</param>
    /// <response code="200">Роль успешно обновлена</response>
    /// <response code="404">Роль с указанным id не найдена</response>
    /// <response code="409">В списке прав указан неизвестный код</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageRoles)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleResponse>> Update(int id, [FromBody] UpdateRoleRequest request)
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
    /// Удалить роль
    /// </summary>
    /// <param name="id">Идентификатор роли</param>
    /// <response code="204">Роль успешно удалена</response>
    /// <response code="404">Роль с указанным id не найдена</response>
    /// <response code="409">Роль назначена пользователям — удаление невозможно</response>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageRoles)]
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