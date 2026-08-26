namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndRedactionResponse
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public int Number { get; set; }
    
    public bool IsCurrent { get; set; } // Является ли последней актуальной
    
    public string? Description { get; set; }

    public int DocFileRuId { get; set; }
    public int? DocFileKgId { get; set; }
    public int? DocFileEnId { get; set; }

    /// <summary>Когда документ на соответствующем языке в последний раз заменялся файлом —
    /// null, если это исходный файл редакции (ни разу не заменялся после создания).</summary>
    public DateTime? DocRuUpdatedAt { get; set; }
    public DateTime? DocKgUpdatedAt { get; set; }
    public DateTime? DocEnUpdatedAt { get; set; }

    /// <summary>Таблица изменений и дополнений — null, если для этой редакции ТИД не требовался
    /// (первая редакция документа)</summary>
    public int? TidFileId { get; set; }

    public bool RequiresApproval { get; set; }
    public required string ApprovalStatus { get; set; }

    /// <summary>Оставлено для обратной совместимости — те же id, что и FileId в Attachments.
    /// Новый код должен использовать Attachments (там же есть настоящее имя файла).</summary>
    public List<int> AttachmentFileIds { get; set; } = [];

    /// <summary>Прочие вложения редакции — с оригинальным именем файла (как при загрузке),
    /// чтобы показывать и скачивать его под тем же именем, а не "Вложение #id".</summary>
    public List<VndRedactionAttachmentResponse> Attachments { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}

public class VndRedactionAttachmentResponse
{
    public int FileId { get; set; }
    public required string FileName { get; set; }
    public long SizeBytes { get; set; }
}