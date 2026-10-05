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
/// "Проверка подлинности согласования ВНД": загружается файл редакции, в ответ — редакции, текстом
/// которых он был, и кто, когда, с каким решением подписал ЭЦП именно этот текст (см.
/// VndAuthenticityService). Ничего не изменяет и файл не сохраняет — только считает его SHA-256.
/// </summary>
[ApiController]
[Route("/api/vnd/authenticity")]
[Tags("ВНД — Проверка подлинности согласования")]
[Authorize]
public class VndAuthenticityController : ControllerBase
{
    private readonly IVndAuthenticityService _service;
    private readonly ICurrentUserService _currentUser;

    public VndAuthenticityController(IVndAuthenticityService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Проверить файл редакции (multipart/form-data, поле <c>file</c>).</summary>
    [HttpPost("check")]
    [RequirePermission(PermissionCode.ViewVnd)]
    [EnableRateLimiting("ecp-verify")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VndAuthenticityCheckResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VndAuthenticityCheckResponse>> Check(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Загрузите файл редакции" });

        try
        {
            return Ok(await _service.CheckAsync(file, _currentUser.UserId, ct));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
