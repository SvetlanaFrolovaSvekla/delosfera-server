using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Files.Services;

public interface IFileStorageService
{
    Task<FileAttachment> SaveAsync(IFormFile file, int userId, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(int fileId, CancellationToken ct = default);
    Task DeleteAsync(int fileId, CancellationToken ct = default);
}