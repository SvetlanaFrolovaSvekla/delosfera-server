using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Sz.Controllers;

/// <summary>Запрос хэша резолюции по записке.</summary>
public class SzResolutionHashRequest
{
    public int ParticipantId { get; set; }
    public ResolutionType Decision { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// ЭЦП ГП «Инфоком» на резолюциях согласующих служебной записки — как у ВНД: backend строит хэш
/// резолюции (решение + комментарий + содержимое записки), клиент подписывает его в ГП (2.5) и
/// присылает подпись вместе с решением; backend проверяет её целиком (2.6) и только тогда выносит
/// резолюцию маршрута. Ошибки проверки — { message, code } с кодами ResolutionSignatureErrorCodes
/// (например, 409 + "document_changed" — подписать заново), как у ВНД.
/// </summary>
[ApiController]
[Route("api/sz/{id:int}/ecp")]
[Tags("СЗ — Подпись ЭЦП")]
[Authorize]
public class SzEcpController : ControllerBase
{
    private readonly ISzResolutionSignatureService _signatures;
    private readonly IUserEcpCredentialService _credentials;
    private readonly ISzService _sz;
    private readonly ICurrentUserService _currentUser;
    private readonly EcpSigningOptions _ecpSigning;

    public SzEcpController(
        ISzResolutionSignatureService signatures,
        IUserEcpCredentialService credentials,
        ISzService sz,
        ICurrentUserService currentUser,
        IOptions<EcpSigningOptions> ecpSigning)
    {
        _ecpSigning = ecpSigning.Value;
        _signatures = signatures;
        _credentials = credentials;
        _sz = sz;
        _currentUser = currentUser;
    }

    /// <summary>Хэш резолюции для подписи (2.5). Только активному участнику маршрута записки.</summary>
    [HttpPost("resolution-hash")]
    public Task<IActionResult> ResolutionHash(int id, [FromBody] SzResolutionHashRequest request, CancellationToken ct) =>
        Run(async () => Ok(await _signatures.GetHashAsync(
            id, request.ParticipantId, request.Decision, request.Comment, _currentUser.UserId, ct)));

    /// <summary>Подписать резолюцию сохранённой идентификацией (личный кабинет → "Электронная подпись") —
    /// без окна ПИН/кода. Возвращает поля, которые клиент прикладывает к POST .../resolve.</summary>
    [HttpPost("sign-stored")]
    [EnableRateLimiting("ecp-verify")]
    public Task<IActionResult> SignStored(int id, [FromBody] SzStoredEcpSignRequest request, CancellationToken ct) =>
        Run(async () => Ok(await _credentials.SignPreparedHashAsync(
            _currentUser.UserId,
            async c => (await _signatures.GetHashAsync(
                id, request.ParticipantId, request.Decision, request.Comment, _currentUser.UserId, c)).Hash,
            ct)));

    /// <summary>Резолюция согласующего с подписью ЭЦП. Возвращает обновлённую карточку записки.</summary>
    [HttpPost("resolve")]
    [EnableRateLimiting("ecp-verify")]
    public Task<IActionResult> Resolve(int id, [FromBody] SzEcpResolveRequest request, CancellationToken ct) =>
        Run(async () =>
        {
            await _signatures.ResolveWithEcpAsync(id, request, _currentUser.UserId,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(), ct);
            return Ok(await _sz.GetAsync(id));
        });

    /// <summary>Журнал подписей ЭЦП по записке: кто, когда, ФИО из сертификата, под какой версией текста.</summary>
    [HttpGet("signatures")]
    public Task<IActionResult> Signatures(int id, CancellationToken ct) =>
        Run(async () => Ok(await _signatures.ListAsync(id, _currentUser.UserId, ct)));

    /// <summary>Повторная проверка подписи (2.6 по сохранённой подписи + сверка с текущей запиской).</summary>
    [HttpPost("signatures/{signatureId:int}/verify")]
    [EnableRateLimiting("ecp-verify")]
    public Task<IActionResult> Verify(int id, int signatureId, CancellationToken ct) =>
        Run(async () => Ok(await _signatures.ReverifyAsync(id, signatureId, _currentUser.UserId, ct)));

    /// <summary>Включён ли тестовый режим имитации ЭЦП для служебных записок (EcpSigning:AllowSzTestEcp) —
    /// по нему клиент показывает кнопки «… (тест ЭЦП)».</summary>
    [HttpGet("~/api/sz/ecp/test-mode")]
    public IActionResult TestMode() => Ok(new { allowSzTestEcp = _ecpSigning.AllowSzTestEcp });

    /// <summary>ТЕСТ: решение с имитацией ЭЦП (весь путь ПИН → код → токен → подпись пропускается). Только
    /// при EcpSigning:AllowSzTestEcp=true, иначе 403. В листе согласования такая подпись печатается как
    /// «ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП».</summary>
    [HttpPost("resolve-test")]
    public Task<IActionResult> ResolveTest(int id, [FromBody] SzStoredEcpSignRequest request, CancellationToken ct)
    {
        if (!_ecpSigning.AllowSzTestEcp)
            return Task.FromResult<IActionResult>(new ObjectResult(new
            {
                message = "Тестовая имитация ЭЦП выключена на сервере (EcpSigning:AllowSzTestEcp)",
                code = "ecp_required",
            }) { StatusCode = StatusCodes.Status403Forbidden });

        return Run(async () =>
        {
            await _signatures.ResolveWithTestEcpAsync(id, request, _currentUser.UserId,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(), ct);
            return Ok(await _sz.GetAsync(id));
        });
    }

    private static async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ResolutionSignatureException ex) { return new ObjectResult(new { message = ex.Message, code = ex.Code }) { StatusCode = ex.StatusCode }; }
        catch (EcpCredentialException ex) { return new ObjectResult(new { message = ex.Message, code = ex.Code }) { StatusCode = ex.StatusCode }; }
        catch (KeyNotFoundException ex) { return new NotFoundObjectResult(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden }; }
        catch (InvalidOperationException ex) { return new ConflictObjectResult(new { message = ex.Message }); }
    }
}
