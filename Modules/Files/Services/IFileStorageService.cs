using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Files.Services;

public interface IFileStorageService
{
    Task<FileAttachment> SaveAsync(IFormFile file, int userId, CancellationToken ct = default);

    /// <summary>Сохраняет содержимое, сгенерированное самим сервером (не загруженное
    /// пользователем) — напр. Лист согласования (см. ApprovalSheetGenerator). В отличие от
    /// SaveAsync, не проверяет расширение/сигнатуру содержимого (содержимое доверенное, не
    /// пользовательский ввод).</summary>
    Task<FileAttachment> SaveGeneratedAsync(
        byte[] content, string fileName, string contentType, int userId, CancellationToken ct = default);

    Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(int fileId, CancellationToken ct = default);
    Task DeleteAsync(int fileId, CancellationToken ct = default);

    /// <summary>SHA-256 содержимого файла (hex, нижний регистр) — не трогает хранилище, только
    /// читает переданный поток. Используется вызывающим кодом (см. VndService.AddRedactionAsync)
    /// для дедупликации вложений ДО загрузки в MinIO: если хеш совпадает с уже сохранённым файлом,
    /// новую загрузку можно не делать, а переиспользовать существующий FileAttachmentId.</summary>
    Task<string> ComputeHashAsync(IFormFile file, CancellationToken ct = default);
}