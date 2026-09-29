using delosfera_server.Modules.Signing.Models.InfocomEcp;
using delosfera_server.Modules.Signing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace delosfera_server.Modules.Signing.Controllers;

/// <summary>
/// Проксирует 6 методов ГП «Инфоком» дальше в delosfera-ecp-service (SIG-ECP). Схема сети:
/// клиент → delosfera-server (10.222.10.42) → delosfera-ecp-service (185.88.32.25) →
/// cdsapi-test.srs.kg (185.229.39.40). Сервер сам к ГП Инфоком не обращается.
///
/// ВАЖНО (см. документацию провайдера): ЭП нельзя использовать как первый фактор входа —
/// только как второй/третий при уже выполненном в системе действии. Поэтому эти эндпоинты
/// не заменяют /auth/login и требуют уже действующей сессии delosfera ([Authorize]) — ЭЦП
/// здесь применяется как доп. подтверждение конкретного действия (см. резолюция "Согласовать"
/// в VndApprovalController/VndApprovalService — TODO там же).
/// </summary>
[ApiController]
[Route("api/signing/infocom")]
[Tags("Подписание — ГП Инфоком (ЭЦП)")]
[Authorize]
public class InfocomEcpController : ControllerBase
{
    private readonly IInfocomEcpClient _client;
    private readonly ILogger<InfocomEcpController> _logger;

    public InfocomEcpController(IInfocomEcpClient client, ILogger<InfocomEcpController> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>2.1 Получение метода авторизации.</summary>
    [HttpPost("auth-methods")]
    public Task<IActionResult> AuthMethods([FromBody] InfocomUserAuthMethodsRequest request, CancellationToken ct) =>
        Run("auth-methods", request.PersonIdnp, () => _client.UserAuthMethodsAsync(request, ct));

    /// <summary>2.2 Запрос на отправку пин-кода по выбранному методу. Ограничено (см. "ecp-pin"
    /// в Program.cs) — у провайдера свой счётчик попыток, но не стоит долбить его API запросами
    /// пин-кода чаще необходимого.</summary>
    [HttpPost("pin-code")]
    [EnableRateLimiting("ecp-pin")]
    public Task<IActionResult> PinCode([FromBody] InfocomGetPinCodeRequest request, CancellationToken ct) =>
        Run($"pin-code method={request.Method}", request.PersonIdnp, () => _client.GetPinCodeAsync(request, ct));

    /// <summary>2.3 Получение токена доступа (живёт ~1 минуту). Тот же лимит, что и на pin-code -
    /// перебор пин-кода идёт именно здесь ("Неверный ПИН-код! Осталось попыток: N").</summary>
    [HttpPost("token")]
    [EnableRateLimiting("ecp-pin")]
    public Task<IActionResult> Token([FromBody] InfocomAccountAuthRequest request, CancellationToken ct) =>
        Run("token", request.PersonIdnp, () => _client.AccountAuthAsync(request, ct));

    /// <summary>2.4 Получение сведений о сертификате по токену доступа.</summary>
    [HttpPost("cert-info")]
    public Task<IActionResult> CertInfo([FromBody] InfocomGetCertInfoRequest request, CancellationToken ct) =>
        Run("cert-info", null, () => _client.GetCertInfoAsync(request, ct));

    /// <summary>2.5 Подписание хэша выданным токеном доступа.</summary>
    [HttpPost("sign-hash")]
    public Task<IActionResult> SignHash([FromBody] InfocomSignHashRequest request, CancellationToken ct) =>
        Run("sign-hash", null, () => _client.SignHashAsync(request, ct));

    /// <summary>2.6 Проверка подписи по хэшу.</summary>
    [HttpPost("check-sign")]
    public Task<IActionResult> CheckSign([FromBody] InfocomCheckSignRequest request, CancellationToken ct) =>
        Run("check-sign", null, () => _client.CheckSignAsync(request, ct));

    /// <summary>Пишет в лог: кто (логин из сессии), какая операция, замаскированный ПИН, итог и ответ
    /// провайдера. Сам ПИН, коды из СМС, токены и хэши в лог не попадают.</summary>
    private async Task<IActionResult> Run(string operation, string? pin, Func<Task<System.Text.Json.JsonElement>> action)
    {
        var user = User.Identity?.Name
                   ?? User.FindFirst("sub")?.Value
                   ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                   ?? "unknown";
        var maskedPin = MaskPin(pin);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await action();
            _logger.LogInformation("ЭЦП {Operation}: user={User} pin={Pin} → OK ({Ms} мс)",
                operation, user, maskedPin, sw.ElapsedMilliseconds);
            return Ok(result);
        }
        catch (InfocomEcpException ex)
        {
            _logger.LogWarning(
                "ЭЦП {Operation}: user={User} pin={Pin} → отказ провайдера HTTP {Status}, errorCode={ErrorCode}, message={Message} ({Ms} мс)",
                operation, user, maskedPin, ex.StatusCode, ex.Error.ErrorCode, ex.Error.ErrorMessage, sw.ElapsedMilliseconds);
            return StatusCode(ex.StatusCode == 0 ? 502 : ex.StatusCode, ex.Error);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ЭЦП {Operation}: user={User} pin={Pin} → не удалось обратиться к delosfera-ecp-service",
                operation, user, maskedPin);
            return StatusCode(502, new InfocomErrorBody
            {
                ErrorCode = 502,
                ErrorMessage = "Нет связи с delosfera-ecp-service — проверьте, что сервис запущен (InfocomEcpService:BaseUrl в appsettings.json)",
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "ЭЦП {Operation}: user={User} pin={Pin} → таймаут delosfera-ecp-service ({Ms} мс)",
                operation, user, maskedPin, sw.ElapsedMilliseconds);
            return StatusCode(504, new InfocomErrorBody { ErrorCode = 504, ErrorMessage = "Таймаут delosfera-ecp-service" });
        }
    }

    /// <summary>Оставляет только последние 4 цифры ПИН — достаточно, чтобы найти запись, но не раскрывает номер.</summary>
    private static string MaskPin(string? pin) =>
        string.IsNullOrEmpty(pin) ? "-" : pin.Length <= 4 ? "****" : new string('*', pin.Length - 4) + pin[^4..];
}
