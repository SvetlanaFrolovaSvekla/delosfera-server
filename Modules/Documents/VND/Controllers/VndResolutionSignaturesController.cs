using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Журнал подписей ЭЦП ГП «Инфоком» под резолюциями "Согласовать" (см. VndResolutionSignature) —
/// ТОЛЬКО чтение и повторная проверка. Эндпоинтов изменения/удаления нет и не будет: журнал
/// неизменяем. Права — как у просмотра хода согласования (ViewVnd), плюс правило видимости
/// черновиков (см. ResolutionSignatureService.EnsureCanViewAsync).
/// </summary>
[ApiController]
[Route("/api/vnd/{vndId:int}/signatures")]
[Tags("ВНД — Журнал подписей ЭЦП")]
[Authorize]
[VndSecrecyGuard()]
public class VndResolutionSignaturesController : ControllerBase
{
    private readonly IResolutionSignatureService _service;
    private readonly ICurrentUserService _currentUser;

    public VndResolutionSignaturesController(IResolutionSignatureService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Журнал подписей по документу: кто, когда, ФИО из сертификата, результат проверки.</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(List<VndResolutionSignatureResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VndResolutionSignatureResponse>>> List(int vndId, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ListAsync(vndId, _currentUser.UserId, ct));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }

    /// <summary>Повторная проверка подписи: пересчитывает компоненты хэша из текущего состояния
    /// документа/резолюции и снова вызывает 2.6 по сохранённой подписи. Ничего не изменяет —
    /// результат только возвращается (журнал неизменяем).</summary>
    [HttpPost("{signatureId:int}/verify")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [EnableRateLimiting("ecp-verify")]
    [ProducesResponseType(typeof(ResolutionSignatureVerifyResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResolutionSignatureVerifyResponse>> Verify(
        int vndId, int signatureId, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ReverifyAsync(vndId, signatureId, _currentUser.UserId, ct));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }
}
