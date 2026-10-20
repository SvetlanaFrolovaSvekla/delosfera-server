using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Substitutions.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Substitutions.Controllers;

/// <summary>
/// Этап «На подписании» заявки на замещение: зафиксированные приказ и договор о матответственности,
/// подписи ЭЦП ГП «Инфоком» директора, отсутствующего и замещающего — тот же порядок, что у резолюций
/// СЗ (SzEcpController): backend строит хэш, клиент подписывает его в ГП (2.5) и присылает подпись,
/// backend проверяет её (2.6) и только тогда записывает в журнал. Ошибки проверки — { message, code }
/// с кодами ResolutionSignatureErrorCodes (например, 409 + "document_changed" — подписать заново).
/// </summary>
[ApiController]
[Authorize]
[Route("api/substitution-requests/{id:int}/signing")]
[Tags("Заявки на замещение — подписание ЭЦП")]
public class SubstitutionSigningController : ControllerBase
{
    private readonly ISubstitutionSigningService _signing;
    private readonly ISubstitutionService _requests;
    private readonly IUserEcpCredentialService _credentials;
    private readonly ICurrentUserService _currentUser;
    private readonly EcpSigningOptions _ecp;

    public SubstitutionSigningController(
        ISubstitutionSigningService signing, ISubstitutionService requests,
        IUserEcpCredentialService credentials, ICurrentUserService currentUser,
        IOptions<EcpSigningOptions> ecp)
    {
        _signing = signing;
        _requests = requests;
        _credentials = credentials;
        _currentUser = currentUser;
        _ecp = ecp.Value;
    }

    /// <summary>Документы, места подписей и кто что уже подписал. Тем, кто видит заявку.</summary>
    [HttpGet]
    public Task<IActionResult> State(int id, CancellationToken ct) =>
        Run(id, ct, async () => Ok(await _signing.GetStateAsync(id, _currentUser.UserId, ct)));

    /// <summary>Хэш для подписи (2.5) — только владельцу места подписи.</summary>
    [HttpPost("hash")]
    public Task<IActionResult> Hash(int id, [FromBody] SubstitutionSignTarget target, CancellationToken ct) =>
        Run(id, ct, async () => Ok(new
        {
            hash = await _signing.GetHashAsync(id, target, _currentUser.UserId, ct),
            schemaVersion = SubstitutionSigningService.HashSchemaVersion,
        }));

    /// <summary>Подписать сохранённой идентификацией (личный кабинет → «Электронная подпись») —
    /// без окна ПИН/кода. Возвращает поля, которые клиент прикладывает к POST .../sign.</summary>
    [HttpPost("sign-stored")]
    [EnableRateLimiting("ecp-verify")]
    public Task<IActionResult> SignStored(int id, [FromBody] SubstitutionSignTarget target, CancellationToken ct) =>
        Run(id, ct, async () => Ok(await _credentials.SignPreparedHashAsync(
            _currentUser.UserId, c => _signing.GetHashAsync(id, target, _currentUser.UserId, c), ct)));

    /// <summary>Подпись документа ЭЦП. Возвращает обновлённое состояние подписания.</summary>
    [HttpPost("sign")]
    [EnableRateLimiting("ecp-verify")]
    public Task<IActionResult> Sign(int id, [FromBody] SubstitutionEcpSignRequest request, CancellationToken ct) =>
        Run(id, ct, async () =>
        {
            await _signing.SignAsync(id, request, _currentUser.UserId,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
            return Ok(await _signing.GetStateAsync(id, _currentUser.UserId, ct));
        });

    /// <summary>ТЕСТ: подпись с имитацией ЭЦП — только при EcpSigning:AllowSubstitutionTestEcp=true, иначе 403.
    /// В подписанной версии документа печатается как «ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП».</summary>
    [HttpPost("sign-test")]
    public Task<IActionResult> SignTest(int id, [FromBody] SubstitutionSignTarget target, CancellationToken ct)
    {
        if (!_ecp.AllowSubstitutionTestEcp)
            return Task.FromResult<IActionResult>(new ObjectResult(new
            {
                message = "Тестовая имитация ЭЦП выключена на сервере (EcpSigning:AllowSubstitutionTestEcp)",
                code = "ecp_required",
            }) { StatusCode = StatusCodes.Status403Forbidden });

        return Run(id, ct, async () =>
        {
            await _signing.SignTestAsync(id, target, _currentUser.UserId,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
            return Ok(await _signing.GetStateAsync(id, _currentUser.UserId, ct));
        });
    }

    /// <summary>Файл документа: signed=false — зафиксированный файл как есть (для подписи от руки),
    /// signed=true — он же с листом подписей ЭЦП.</summary>
    [HttpGet("documents/{documentId:int}")]
    public Task<IActionResult> Download(int id, int documentId, [FromQuery] bool signed, CancellationToken ct) =>
        Run(id, ct, async () =>
        {
            var (bytes, name) = await _signing.DownloadAsync(id, documentId, signed, ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", name);
        });

    /// <summary>Макет ЭЦП-версии для окна выбора места подписи: поставленные подписи на своих местах и
    /// закладки-якоря dsa_N на абзацах текста.</summary>
    [HttpGet("documents/{documentId:int}/layout")]
    public Task<IActionResult> Layout(int id, int documentId, CancellationToken ct) =>
        Run(id, ct, async () =>
        {
            var (bytes, name) = await _signing.LayoutAsync(id, documentId, ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", name);
        });

    /// <summary>Видимость — как у карточки заявки (паспорт, ИНН, адреса), затем само действие;
    /// ошибки проверки подписи — { message, code }.</summary>
    private async Task<IActionResult> Run(int id, CancellationToken ct, Func<Task<IActionResult>> action)
    {
        var manage = _currentUser.HasPermission(PermissionCode.ManageSystemSettings)
                     || await _requests.IsHrManagerAsync(_currentUser.UserId, ct);
        if (!await _requests.CanViewAsync(id, _currentUser.UserId, manage, ct))
            return StatusCode(403, new { message = "Нет доступа к этой заявке на замещение" });

        try { return await action(); }
        catch (ResolutionSignatureException ex) { return new ObjectResult(new { message = ex.Message, code = ex.Code }) { StatusCode = ex.StatusCode }; }
        catch (EcpCredentialException ex) { return new ObjectResult(new { message = ex.Message, code = ex.Code }) { StatusCode = ex.StatusCode }; }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
