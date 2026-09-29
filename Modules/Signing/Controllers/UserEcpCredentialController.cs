using System.ComponentModel.DataAnnotations;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Signing.Models.InfocomEcp;
using delosfera_server.Modules.Signing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace delosfera_server.Modules.Signing.Controllers;

public class SaveEcpCredentialRequest
{
    [Required, RegularExpression(InfocomFieldRules.PinPattern, ErrorMessage = InfocomFieldRules.PinError)]
    public required string PersonIdnp { get; set; }

    /// <summary>ИНН организации — необязателен; заполняется, если подписывают от имени организации.</summary>
    [StringLength(20)]
    public string? OrganizationInn { get; set; }

    /// <summary>Код из СМС/почты (2.2 → 2.3 byPin).</summary>
    [Required, StringLength(32, MinimumLength = 1)]
    public required string Code { get; set; }
}

public class SignResolutionWithStoredEcpRequest
{
    public int VndId { get; set; }
    public int StageId { get; set; }
    /// <summary>Итоговый текст резолюции (так же, как для resolution-hash).</summary>
    public string? Comment { get; set; }
}

/// <summary>
/// Сохранённая ЭЦП-идентификация пользователя ("личный кабинет → Электронная подпись"): один раз ввести
/// ПИН и код из СМС/почты — дальше сервер сам обменивает код на минутный токен при каждом подписании
/// резолюции (см. UserEcpCredentialService). Код хранится зашифрованно, срок ограничен, отзывается
/// пользователем. Все эндпоинты работают только с записью ТЕКУЩЕГО пользователя.
/// </summary>
[ApiController]
[Route("api/signing/ecp-credential")]
[Tags("Подписание — ГП Инфоком (сохранённая идентификация)")]
[Authorize]
public class UserEcpCredentialController : ControllerBase
{
    private readonly IUserEcpCredentialService _service;
    private readonly ICurrentUserService _currentUser;

    public UserEcpCredentialController(IUserEcpCredentialService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<EcpCredentialStatusResponse>> Status(CancellationToken ct) =>
        Ok(await _service.GetStatusAsync(_currentUser.UserId, ct));

    /// <summary>Сохранить идентификацию: код проверяется обменом на токен (2.3) — тот же лимит "ecp-pin",
    /// что и на прямой запрос токена (у ГП счётчик неверных попыток).</summary>
    [HttpPut]
    [EnableRateLimiting("ecp-pin")]
    public Task<IActionResult> Save([FromBody] SaveEcpCredentialRequest request, CancellationToken ct) =>
        Run(async () => Ok(await _service.SaveAsync(
            _currentUser.UserId, request.PersonIdnp, request.OrganizationInn, request.Code, ct)));

    [HttpDelete]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        await _service.RevokeAsync(_currentUser.UserId, ct);
        return NoContent();
    }

    /// <summary>Подписать резолюцию "Согласовать" сохранённой идентификацией. Возвращает поля, которые
    /// клиент прикладывает к POST .../decision. Лимит "ecp-verify" — дорогая операция (хэш файлов + 2 запроса к ГП).</summary>
    [HttpPost("sign-resolution")]
    [EnableRateLimiting("ecp-verify")]
    public Task<IActionResult> SignResolution([FromBody] SignResolutionWithStoredEcpRequest request, CancellationToken ct) =>
        Run(async () => Ok(await _service.SignResolutionAsync(
            _currentUser.UserId, request.VndId, request.StageId, request.Comment, ct)));

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (EcpCredentialException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message, code = ex.Code }); }
        catch (ResolutionSignatureException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message, code = ex.Code }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }
}
