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

    /// <summary>Скачать файл по id</summary>
    [HttpGet("{fileId:int}")]
    public async Task<IActionResult> Download(int fileId, CancellationToken ct)
    {
        // Проверка доступа до отдачи файла — иначе любой пользователь качает любой файл по id (IDOR).
        if (!await _accessAuthorizer.CanCurrentUserAccessAsync(fileId, ct))
            return Forbid();

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