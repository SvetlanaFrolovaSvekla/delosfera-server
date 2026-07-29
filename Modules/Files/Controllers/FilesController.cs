using delosfera_server.Modules.Files.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace delosfera_server.Modules.Files.Controllers;

[ApiController]
[Route("/files")]
[Tags("Файлы")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileService;

    public FilesController(IFileStorageService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>Скачать файл по id</summary>
    [HttpGet("{fileId:int}")]
    public async Task<IActionResult> Download(int fileId)
    {
        try
        {
            var (stream, contentType, fileName) = await _fileService.DownloadAsync(fileId);
            return File(stream, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}