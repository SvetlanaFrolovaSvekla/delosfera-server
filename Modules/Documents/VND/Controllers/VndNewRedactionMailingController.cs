using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Раздел «Рассылка с уведомлением о новых редакциях» на странице «Настройки рассылок по
/// актуализации ВНД»: включатель, каналы доставки и состав получателей. Само письмо уходит при
/// каждой консолидации ВНД (см. IVndNewRedactionMailingService.NotifyNewRedactionAsync).
/// Права — те же, что у остальных разделов этой страницы.
/// </summary>
[ApiController]
[Route("api/vnd/new-redaction-mailing")]
[Tags("ВНД — Рассылка о новых редакциях")]
[Authorize]
[RequirePermission(PermissionCode.ManageVndDictionaries, PermissionCode.ManageVndActualizationMailing)]
public class VndNewRedactionMailingController : ControllerBase
{
    private readonly IVndNewRedactionMailingService _service;

    public VndNewRedactionMailingController(IVndNewRedactionMailingService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<VndNewRedactionMailingResponse>> Get(CancellationToken ct) =>
        Ok(await _service.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<VndNewRedactionMailingResponse>> Update(
        [FromBody] UpdateVndNewRedactionMailingRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.UpdateAsync(request, ct));
        }
        catch (KeyNotFoundException ex) { return NotFound(new {message = ex.Message}); }
        catch (InvalidOperationException ex) { return BadRequest(new {message = ex.Message}); }
    }
}
