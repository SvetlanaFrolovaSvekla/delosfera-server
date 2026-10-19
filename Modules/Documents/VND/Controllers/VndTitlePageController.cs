using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Documents.VND.Controllers;

/// <summary>Титульный лист редакции (специальное вложение, см. VndTitlePageService): сформировать по
/// реквизитам, загрузить свой файл или убрать. Скачивание/просмотр — как у остальных файлов редакции,
/// через /api/files/{TitlePageFileId}.</summary>
[ApiController]
[Route("api/vnd/{vndId:int}/redactions/{redactionId:int}/title-page")]
[Tags("ВНД — Титульный лист")]
[Authorize]
[VndSecrecyGuard()]
public class VndTitlePageController : ControllerBase
{
    private readonly IVndTitlePageService _titlePages;
    private readonly IVndService _vndService;
    private readonly ICurrentUserService _currentUser;

    public VndTitlePageController(IVndTitlePageService titlePages, IVndService vndService,
        ICurrentUserService currentUser)
    {
        _titlePages = titlePages;
        _vndService = vndService;
        _currentUser = currentUser;
    }

    /// <summary>Сформировать (или сформировать заново) титульный лист по реквизитам редакции.</summary>
    [HttpPost("generate")]
    [RequirePermission(PermissionCode.ViewVnd)]
    public Task<IActionResult> Generate(int vndId, int redactionId) =>
        Run(vndId, redactionId, () => _titlePages.GenerateAsync(vndId, redactionId, _currentUser.UserId));

    /// <summary>Загрузить свой файл титульного листа (заменяет сформированный системой).</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.ViewVnd)]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> Upload(int vndId, int redactionId, IFormFile file) =>
        Run(vndId, redactionId, () => _titlePages.UploadAsync(vndId, redactionId, file, _currentUser.UserId));

    /// <summary>Убрать титульный лист у редакции.</summary>
    [HttpDelete]
    [RequirePermission(PermissionCode.ViewVnd)]
    public Task<IActionResult> Remove(int vndId, int redactionId) =>
        Run(vndId, redactionId, () => _titlePages.RemoveAsync(vndId, redactionId, _currentUser.UserId));

    /// <summary>Выполняет действие и возвращает обновлённую редакцию (VndRedactionResponse).</summary>
    private async Task<IActionResult> Run(int vndId, int redactionId, Func<Task> action)
    {
        try
        {
            await action();
            var redactions = await _vndService.GetRedactionsAsync(vndId);
            var updated = redactions.FirstOrDefault(r => r.Id == redactionId);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
