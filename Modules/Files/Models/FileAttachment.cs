using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Files.Models;

public class FileAttachment : IAuditableEntity
{
    public int Id { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string StorageKey { get; set; } // object name в бакете
    public string Bucket { get; set; } = "delosfera-vnd";

    /// <summary>SHA-256 содержимого файла (hex, нижний регистр) — используется, чтобы не грузить
    /// в хранилище дубль уже существующего файла (см. VndService.AddRedactionAsync: вложения
    /// редакции ВНД не дублируются, если совпадают по содержимому с уже приложенными к этому
    /// же ВНД). Заполняется при загрузке (см. MinioFileStorageService.SaveAsync).</summary>
    public string? Hash { get; set; }

    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}