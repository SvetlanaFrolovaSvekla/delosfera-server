using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Modules.Coauthoring.DTO;
using delosfera_server.Modules.Coauthoring.Services;

namespace delosfera_server.Modules.Coauthoring.Controllers;

/// <summary>
/// Онлайн-соредактирование вложений через OnlyOffice Document Server (COOP-1).
///
/// Три эндпоинта разного назначения:
/// • config — для клиента (авторизация JWT приложения), выдаёт конфиг редактора;
/// • download / callback — для самого Document Server (авторизация подписью
///   OnlyOffice:JwtSecret), поэтому [AllowAnonymous]: DS ходит server-to-server и
///   токеном приложения не располагает.
/// </summary>
[ApiController]
[Route("api/coauthoring")]
[Tags("Соредактирование")]
public class CoauthoringController : ControllerBase
{
    private readonly ICoauthoringService _service;
    private readonly ILogger<CoauthoringController> _logger;

    public CoauthoringController(ICoauthoringService service, ILogger<CoauthoringController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Конфиг редактора OnlyOffice для вложения карточки (подписан JWT).</summary>
    [HttpGet("config/{attachmentId:int}")]
    [Authorize]
    public async Task<IActionResult> Config(int attachmentId, CancellationToken ct)
    {
        try
        {
            var apiBaseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            return Ok(await _service.BuildConfigAsync(attachmentId, apiBaseUrl, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Отдаёт текущее содержимое файла Document Server'у по подписанной временной
    /// ссылке. Авторизация — подписью в token, не сессией пользователя.
    /// </summary>
    [HttpGet("download/{attachmentId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(int attachmentId, [FromQuery] string token, CancellationToken ct)
    {
        try
        {
            var (stream, contentType, fileName) = await _service.OpenForDownloadAsync(attachmentId, token, ct);
            return File(stream, contentType, fileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Callback OnlyOffice: на status=2/6 сохраняет отредактированный файл новой
    /// версией. Отвечает форматом, который ждёт DS: { "error": 0 } при успехе.
    /// </summary>
    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] int attachmentId, [FromBody] OnlyOfficeCallbackRequest body, CancellationToken ct)
    {
        try
        {
            // DS кладёт JWT либо в тело (JWT_IN_BODY=true), либо в заголовок Authorization
            // (Bearer). Поддерживаем оба: если тела нет — берём токен из заголовка.
            if (string.IsNullOrWhiteSpace(body.Token))
            {
                var header = Request.Headers.Authorization.ToString();
                if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    body.Token = header["Bearer ".Length..].Trim();
            }

            await _service.HandleCallbackAsync(attachmentId, body, ct);
            return Ok(new { error = 0 });
        }
        catch (UnauthorizedAccessException)
        {
            // DS ждёт JSON с ненулевым error; 401 он трактует иначе.
            return Ok(new { error = 1 });
        }
        catch (Exception ex)
        {
            // Не роняем 500 наружу DS: он будет ретраить и копить незакрытые сессии.
            // Возвращаем error=1 и пишем причину в лог.
            _logger.LogError(ex, "Coauthoring callback: ошибка сохранения вложения {AttachmentId}", attachmentId);
            return Ok(new { error = 1 });
        }
    }
}
