using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delosfera_server.Common.Authorization;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>"Сформировать документ для печати" — кнопка в шапке открытого ВНД рядом с глазиком и
/// звёздочкой: выбранные тексты редакции, специальные вложения и вложения склеиваются в один .docx
/// в выбранном порядке (см. VndPrintBundleService).</summary>
[ApiController]
[Route("api/vnd/{vndId:int}/print-bundle")]
[Tags("ВНД — Документ для печати")]
[Authorize]
[VndSecrecyGuard()]
public class VndPrintBundleController : ControllerBase
{
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IVndPrintBundleService _service;

    public VndPrintBundleController(IVndPrintBundleService service)
    {
        _service = service;
    }

    /// <summary>Собрать один .docx из выбранных файлов редакций ВНД (в порядке FileIds).</summary>
    /// <remarks>Нужно право «Экспорт ВНД» — как и на скачивание каждого из этих файлов по
    /// отдельности. Поддерживаются .docx и изображения (PNG, JPEG).</remarks>
    [HttpPost]
    [RequirePermission(PermissionCode.ViewVnd)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, DocxMime)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Build(int vndId, [FromBody] VndPrintBundleRequest request, CancellationToken ct)
    {
        try
        {
            var (content, fileName) = await _service.BuildAsync(vndId, request, ct);
            return File(content, DocxMime, fileName);
        }
        catch (VndPrintBundleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
