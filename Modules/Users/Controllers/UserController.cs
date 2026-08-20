using delosfera_server.Common.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Common.Services.Authorization.Ldap;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.Users.Controllers;

/// <summary>
/// Управление пользователями системы
/// </summary>
[ApiController]
[Route("api/users")]
[Tags("Пользователи")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _service;
    private readonly IUserActivityService _activityService;
    private readonly ILanguageResolver _languageResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly DelosferaDbContext _db;

    public UserController(
        IUserService service,
        IUserActivityService activityService,
        ILanguageResolver languageResolver,
        ICurrentUserService currentUser,
        DelosferaDbContext db)
    {
        _service = service;
        _activityService = activityService;
        _languageResolver = languageResolver;
        _currentUser = currentUser;
        _db = db;
    }

    /// <summary>Текущий авторизованный пользователь</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        var language = _languageResolver.Resolve(Request);
        return Ok(await _service.GetByIdAsync(_currentUser.UserId, language));
    }
    
    /// <summary>Получить пользователя по id</summary>
    /// <response code="200">Пользователь найден</response>
    /// <response code="404">Пользователь не найден</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(int id)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            return Ok(await _service.GetByIdAsync(id, language));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Лента активности пользователя (созданные ВНД, решения по согласованию, инициированные согласования)</summary>
    /// <response code="200">Активность получена</response>
    /// <response code="403">Нет права смотреть чужую активность</response>
    [HttpGet("{id:int}/activity")]
    [ProducesResponseType(typeof(UserActivityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserActivityResponse>> GetActivity(int id)
    {
        // Свою активность видит каждый; чужую — только с правом управления пользователями.
        if (id != _currentUser.UserId && !_currentUser.HasPermission(PermissionCode.ManageUsers))
            return Forbid();

        var language = _languageResolver.Resolve(Request);
        return Ok(await _activityService.GetActivityAsync(id, language));
    }

    /// <summary>
    /// Получить список всех пользователей
    /// </summary>
    /// <param name="sortBy">Способ сортировки результата</param>
    /// <param name="search">Поиск по ФИО или email/логину</param>
    /// <param name="orgUnitIds">Фильтр по структурным подразделениям (можно указать несколько id)</param>
    /// <param name="positionIds">Фильтр по должностям (можно указать несколько id)</param>
    /// <param name="roleIds">Фильтр по ролям (можно указать несколько id)</param>
    /// <param name="source">Фильтр по источнику учётной записи</param>
    /// <param name="isBlocked">Фильтр по статусу блокировки</param>
    /// <response code="200">Список пользователей получен успешно</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserResponse>>> GetAll(
        [FromQuery] UserSortBy sortBy = UserSortBy.CreatedAtAsc,
        [FromQuery] string? search = null,
        [FromQuery] List<int>? orgUnitIds = null,
        [FromQuery] List<int>? positionIds = null,
        [FromQuery] List<int>? roleIds = null,
        [FromQuery] UserSource? source = null,
        [FromQuery] bool? isBlocked = null)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.GetAllAsync(sortBy, search, orgUnitIds, positionIds, roleIds, source, isBlocked, language);
        return Ok(result);
    }

    /// <summary>
    /// Краткий список сотрудников для выбора: кого назначить согласующим,
    /// исполнителем, подписантом.
    ///
    /// Полный список отдаёт роли, даты блокировки и вложенные объекты должности и
    /// подразделения — на пятистах сотрудниках это полмегабявта на каждое открытие
    /// карточки, а для выбора человека нужны четыре поля. Заблокированные не
    /// возвращаются: назначать им задачу бессмысленно, войти они не могут.
    /// </summary>
    /// <response code="200">Список получен</response>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup(CancellationToken ct) =>
        Ok(await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.BlockedAt == null)
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                position = u.Position != null ? u.Position.TitleRu : null,
                orgUnit = u.OrgUnit != null ? u.OrgUnit.TitleRu : null,
            })
            .ToListAsync(ct));

    /// <summary>
    /// Создать нового пользователя
    /// </summary>
    /// <response code="201">Пользователь успешно создан</response>
    /// <response code="404">Указанная должность, подразделение или роль не найдены</response>
    /// <response code="409">Пользователь с таким email уже существует</response>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageUsers)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _service.CreateAsync(request, language);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
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
    /// Обновить существующего пользователя
    /// </summary>
    /// <response code="200">Пользователь успешно обновлён</response>
    /// <response code="404">Пользователь, должность, подразделение или роль не найдены</response>
    /// <response code="409">Пользователь с таким email уже существует</response>
    [HttpPut("{id:int}")]
    [RequirePermission(PermissionCode.ManageUsers)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Update(int id, [FromBody] UpdateUserRequest request)
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
    /// Заблокировать учётную запись
    /// </summary>
    /// <response code="200">Пользователь заблокирован</response>
    /// <response code="404">Пользователь не найден</response>
    [HttpPost("{id:int}/block")]
    [RequirePermission(PermissionCode.ManageUsers)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> Block(int id, [FromBody] BlockUserRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _service.BlockAsync(id, _currentUser.UserId, request.Reason, language);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Разблокировать учётную запись
    /// </summary>
    /// <response code="200">Пользователь разблокирован</response>
    /// <response code="404">Пользователь не найден</response>
    [HttpPost("{id:int}/unblock")]
    [RequirePermission(PermissionCode.ManageUsers)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> Unblock(int id)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            var result = await _service.UnblockAsync(id, language);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Удалить пользователя
    /// </summary>
    /// <response code="204">Пользователь успешно удалён</response>
    /// <response code="404">Пользователь не найден</response>
    /// <response code="409">Нельзя удалить - на пользователя есть ссылки в других данных</response>
    [HttpDelete("{id:int}")]
    [RequirePermission(PermissionCode.ManageUsers)]
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
    
    /// <summary>Запустить синхронизацию пользователей с LDAP</summary>
    [HttpPost("ldap-sync")]
    [RequirePermission(PermissionCode.ManageUsers)]
    [ProducesResponseType(typeof(LdapSyncResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<LdapSyncResult>> SyncLdap(
        [FromServices] LdapUserSyncService syncService, CancellationToken ct)
    {
        var result = await syncService.SyncAsync(ct);
        return Ok(result);
    }
}