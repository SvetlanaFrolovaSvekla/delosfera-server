using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Notifications.Services;

namespace delosfera_server.Modules.Notifications.Controllers;

/// <summary>Персональные настройки уведомлений (УВ-16).</summary>
[ApiController]
[Route("api/notification-settings")]
[Tags("Уведомления — Настройки")]
[Authorize]
public class NotificationSettingController : ControllerBase
{
    private readonly INotificationSettingService _settings;
    private readonly ICurrentUserService _currentUser;

    public NotificationSettingController(INotificationSettingService settings, ICurrentUserService currentUser)
    {
        _settings = settings;
        _currentUser = currentUser;
    }

    /// <summary>Мои настройки уведомлений.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationSettingDto>> Get() =>
        Ok(await _settings.GetAsync(_currentUser.UserId));

    /// <summary>Изменить мои настройки.</summary>
    [HttpPut]
    public async Task<ActionResult<NotificationSettingDto>> Update([FromBody] NotificationSettingDto dto) =>
        Ok(await _settings.SetAsync(_currentUser.UserId, dto));
}
