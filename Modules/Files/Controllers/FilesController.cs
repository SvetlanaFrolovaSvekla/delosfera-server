using delosfera_server.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Files.Controllers;

[ApiController]
[Route("api/files")]
[Tags("Файлы")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileService;
    private readonly IFileAccessAuthorizer _accessAuthorizer;

    public FilesController(IFileStorageService fileService, IFileAccessAuthorizer accessAuthorizer)
    {
        _fileService = fileService;
        _accessAuthorizer = accessAuthorizer;
    }

    /// <summary>Получить файл по id: для просмотра в браузере или (download=true) для скачивания</summary>
    /// <param name="download">Явное скачивание. Для файлов ВНД требует права «Экспорт ВНД»;
    /// просмотр (без флага) доступен всем, кому виден документ.</param>
    [HttpGet("{fileId:int}")]
    public async Task<IActionResult> Download(int fileId, [FromQuery] bool download = false, CancellationToken ct = default)
    {
        // Проверка доступа до отдачи файла — иначе любой пользователь качает любой файл по id (IDOR).
        if (!await _accessAuthorizer.CanCurrentUserAccessAsync(fileId, ct))
            return Forbid();

        if (download && !await _accessAuthorizer.CanCurrentUserExportAsync(fileId, ct))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Нет права на скачивание документов ВНД (право «Экспорт ВНД»)" });

        try
        {
            var (stream, contentType, fileName) = await _fileService.DownloadAsync(fileId, ct);
            return File(stream, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}