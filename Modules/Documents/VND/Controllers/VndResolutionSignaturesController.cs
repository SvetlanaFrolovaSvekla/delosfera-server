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

    /// <summary>
    /// Файлы для проверки подписи вне системы (например, на сайте провайдера ЭЦП):
    /// <c>file=signature</c> (по умолчанию) — CMS-подпись в base64, "{код}-resolution-{id}.sig.b64";
    /// <c>file=data</c> — подписанные данные, "{код}-resolution-{id}.txt": каноническая строка резолюции
    /// (решение, комментарий, отпечаток текста редакции, код ВНД), SHA-256 которой и подписан.
    ///
    /// Подпись — открытое доказательство: подписать ею что-то другое или предъявить её в системе повторно
    /// нельзя (журнал отклоняет повтор и старые подписи). Но в CMS лежит сертификат подписанта с его
    /// персональными данными, поэтому скачивание — только с правом «Экспорт ВНД», как и у Листа
    /// согласования, плюс правило видимости черновиков.
    /// </summary>
    [HttpGet("{signatureId:int}/download")]
    [RequirePermission(PermissionCode.ExportVnd)]
    [EnableRateLimiting("ecp-verify")]
    public async Task<IActionResult> Download(
        int vndId, int signatureId, [FromQuery] string? file, CancellationToken ct)
    {
        try
        {
            var files = await _service.GetSignatureFilesAsync(vndId, signatureId, _currentUser.UserId, ct);

            if (string.Equals(file, "data", StringComparison.OrdinalIgnoreCase))
                return File(files.SignedData, "text/plain; charset=utf-8", $"{files.BaseName}.txt");

            return File(System.Text.Encoding.ASCII.GetBytes(files.SignatureBase64), "application/octet-stream",
                $"{files.BaseName}.sig.b64");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
