using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.Models;

/// <summary>
/// Вложение документа. Хранит SHA-256 хеш версии файла — основа юридической
/// значимости (VND-08, TID-14, SIG-01): подпись накладывается на хеш версии.
/// </summary>
public class DocumentAttachment : IAuditableEntity
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>Ссылка на файл в файловом хранилище (путь/ключ).</summary>
    public required string FileRef { get; set; }

    public required string FileName { get; set; }

    /// <summary>SHA-256 (Base64) содержимого файла.</summary>
    public required string Hash { get; set; }

    public long Size { get; set; }

    /// <summary>Основной файл документа (в отличие от прочих приложений).</summary>
    public bool IsPrimary { get; set; }

    public int UploadedById { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
