namespace delosfera_server.Modules.Files.Models;

/// <summary>
/// Версия содержимого вложения (COOP-1). Онлайн-соредактирование в OnlyOffice
/// сохраняет результат НОВОЙ версией в MinIO, не перезатирая прежнюю: каждая
/// правка получает собственный объект в хранилище (StorageKey) и запись здесь.
///
/// В системе не было отдельной таблицы версий (VER-1 отсутствует), поэтому заведена
/// минимальная: она хранит историю объектов конкретного <see cref="FileAttachment"/>,
/// а сам FileAttachment всегда указывает на последнюю (актуальную) версию — так
/// прежние скачивания и подписи продолжают работать без изменений.
/// </summary>
public class AttachmentVersion
{
    public int Id { get; set; }

    /// <summary>Файл-вложение, версии которого мы храним.</summary>
    public int FileAttachmentId { get; set; }
    public FileAttachment? FileAttachment { get; set; }

    /// <summary>Порядковый номер версии в пределах одного FileAttachmentId (с 1).</summary>
    public int Version { get; set; }

    /// <summary>Имя объекта этой версии в бакете (не перезатирается новыми версиями).</summary>
    public required string StorageKey { get; set; }

    public string Bucket { get; set; } = "delosfera-vnd";

    public required string ContentType { get; set; }
    public required string OriginalFileName { get; set; }
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 содержимого версии (hex, нижний регистр).</summary>
    public string? Hash { get; set; }

    /// <summary>Кто сохранил версию (при соредактировании — последний правивший).</summary>
    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
