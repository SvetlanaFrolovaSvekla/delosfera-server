using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.DTO.Response;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;

namespace delosfera_server.Modules.Notifications.Controllers;

[ApiController]
[Route("/notifications")]
[Tags("Уведомления")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly ILanguageResolver _languageResolver;
    private readonly ICurrentUserService _currentUser;

    public NotificationController(
        INotificationService service, ILanguageResolver languageResolver, ICurrentUserService currentUser)
    {
        _service = service;
        _languageResolver = languageResolver;
        _currentUser = currentUser;
    }

    /// <summary>Список уведомлений текущего пользователя с фильтрами (категория, прочитано/нет, избранное, поиск)</summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(PagedNotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedNotificationResponse>> Search([FromBody] NotificationFilterRequest request)
    {
        var language = _languageResolver.Resolve(Request);
        var result = await _service.SearchAsync(request, _currentUser.UserId, language);
        return Ok(result);
    }

    /// <summary>Список доступных категорий уведомлений</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<NotificationCategoryResponse>), StatusCodes.Status200OK)]
    public ActionResult<List<NotificationCategoryResponse>> GetCategories()
    {
        var language = _languageResolver.Resolve(Request);
        return Ok(NotificationCategoryCatalog.GetAll(language));
    }

    /// <summary>Получение счётчиков уведомлений: всего непрочитанных, избранных, непрочитанных по категориям</summary>
    [HttpGet("counts")]
    [ProducesResponseType(typeof(NotificationCountsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationCountsResponse>> GetCounts()
    {
        return Ok(await _service.GetCountsAsync(_currentUser.UserId));
    }

    /// <summary>Получение одного уведомления по id (id записи в личном списке пользователя)</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> GetById(int id)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            return Ok(await _service.GetByIdAsync(id, _currentUser.UserId, language));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Создать и разослать уведомление (конкретным пользователям или всем)</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request)
    {
        try
        {
            var id = await _service.CreateAsync(request, _currentUser.UserId);
            return CreatedAtAction(nameof(GetById), new { id }, null);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Отметить уведомление прочитанным</summary>
    [HttpPost("{id:int}/read")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> MarkAsRead(int id)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            return Ok(await _service.MarkAsReadAsync(id, _currentUser.UserId, language));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Снять отметку "прочитано"</summary>
    [HttpPost("{id:int}/unread")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> MarkAsUnread(int id)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            return Ok(await _service.MarkAsUnreadAsync(id, _currentUser.UserId, language));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Прочитать все уведомления (опционально - только в рамках одной категории)</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead([FromQuery] NotificationCategory? category)
    {
        var affected = await _service.MarkAllAsReadAsync(_currentUser.UserId, category);
        return Ok(new { markedCount = affected });
    }

    /// <summary>Добавить/убрать из избранного</summary>
    [HttpPost("{id:int}/favorite")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse>> ToggleFavorite(int id)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            return Ok(await _service.ToggleFavoriteAsync(id, _currentUser.UserId, language));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Удалить уведомление из своего списка (мягкое удаление, других получателей не затрагивает)</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteForUserAsync(id, _currentUser.UserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }
}