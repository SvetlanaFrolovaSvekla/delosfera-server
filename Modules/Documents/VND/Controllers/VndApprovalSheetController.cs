using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>
/// Лист согласования процесса по его текущему состоянию (формируется при каждом запросе — после
/// каждого решения он уже актуален). Два режима: <c>plain</c> — решения и даты, колонка "Подпись"
/// пустая; <c>signed</c> — в колонке "Подпись" штамп "Подписано с помощью ЭЦП" с владельцем,
/// сертификатом, временем и хэшем подписи (из журнала подписей ЭЦП).
/// </summary>
[ApiController]
[Route("/api/vnd/{vndId:int}/approval-sheet")]
[Tags("ВНД — Лист согласования")]
[Authorize]
public class VndApprovalSheetController : ControllerBase
{
    private const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IApprovalSheetService _service;
    private readonly ICurrentUserService _currentUser;

    public VndApprovalSheetController(IApprovalSheetService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>DOCX листа согласования процесса. <paramref name="mode"/>: plain (по умолчанию) или signed.</summary>
    [HttpGet("{processId:int}")]
    [RequirePermission(PermissionCode.ViewVnd)]
    public async Task<IActionResult> Get(int vndId, int processId, [FromQuery] string? mode,
        [FromQuery] bool download, CancellationToken ct)
    {
        // Явное скачивание листа (а не просмотр в окне) — только с правом «Экспорт ВНД».
        if (download && !_currentUser.HasPermission(PermissionCode.ExportVnd))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Нет права на скачивание документов ВНД (право «Экспорт ВНД»)" });

        var sheetMode = string.Equals(mode, "signed", StringComparison.OrdinalIgnoreCase)
            ? ApprovalSheetMode.Signed
            : ApprovalSheetMode.Plain;
        try
        {
            var (content, fileName) = await _service.BuildAsync(vndId, processId, sheetMode, _currentUser.UserId, ct);
            return File(content, WordContentType, fileName);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
    }
}
