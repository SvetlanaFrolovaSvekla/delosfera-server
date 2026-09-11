using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Раздел "Уведомления" → "Настройки рассылок" → "Нормотворчество" (см. ManagementPage на
/// фронте, кнопка "Настройки рассылок плана" на странице "Планирование актуализации"):
/// ответственные сотрудники СП за актуализацию ВНД, ежемесячная сводка им 1-го числа,
/// критические напоминания по настраиваемым порогам и единоразовая рассылка плана актуализации.
///
/// Всё под одним правом ManageVndDictionaries — это раздел администрирования, а не рабочий
/// экран: то же право, которым уже гейтится справочник порогов индикации сроков
/// (ActualizationBucketSettingsController).
/// </summary>
[ApiController]
[Route("api/vnd/actualization-notifications")]
[Tags("ВНД — Уведомления об актуализации")]
[Authorize]
[RequirePermission(PermissionCode.ManageVndDictionaries)]
public class ActualizationNotificationsController : ControllerBase
{
    private readonly IActualizationNotificationService _service;
    private readonly ILanguageResolver _languageResolver;
    private readonly ICurrentUserService _currentUser;

    public ActualizationNotificationsController(
        IActualizationNotificationService service,
        ILanguageResolver languageResolver,
        ICurrentUserService currentUser)
    {
        _service = service;
        _languageResolver = languageResolver;
        _currentUser = currentUser;
    }

    [HttpGet("responsibles")]
    public async Task<IActionResult> GetResponsibles() => Ok(await _service.GetResponsiblesAsync());

    /// <summary>Полная замена состава ответственных для одного СП.</summary>
    [HttpPut("responsibles")]
    public async Task<IActionResult> SetResponsibles([FromBody] SetActualizationNotificationResponsiblesRequest request)
    {
        try
        {
            return Ok(await _service.SetResponsiblesAsync(request));
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() => Ok(await _service.GetSettingsAsync());

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateActualizationNotificationSettingsRequest request)
    {
        try
        {
            return Ok(await _service.UpdateSettingsAsync(request));
        }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }

    /// <summary>Демонстрация письма ежемесячной сводки для одного СП — без отправки.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] int orgUnitId)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            return Ok(await _service.PreviewMonthlyDigestAsync(orgUnitId, language));
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
    }

    /// <summary>Раздел "Создать единоразовую рассылку плана актуализации" — разовое письмо
    /// ответственным сотрудникам выбранных СП (группой) и/или отдельным пользователям, с
    /// опциональным Excel-вложением плана актуализации.</summary>
    [HttpPost("one-time-mailing")]
    public async Task<IActionResult> SendOneTimeMailing([FromBody] SendActualizationOneTimeMailingRequest request)
    {
        var language = _languageResolver.Resolve(Request);

        try
        {
            return Ok(await _service.SendOneTimeMailingAsync(request, _currentUser.UserId, language));
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }
}
