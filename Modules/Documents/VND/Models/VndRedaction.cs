using delosfera_server.Common.Models;
using delosfera_server.Modules.Files.Models;


namespace delosfera_server.Modules.Documents.VND.Models;

public class VndRedaction : IAuditableEntity
{
    public int Id { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }
    
    public string? Description { get; set; } // Описание редакции

    /// <summary>Порядковый номер редакции в рамках ВНД, авто-инкремент (1, 2, 3...)</summary>
    public int Number { get; set; }

    /// <summary>Код редакции, авто: {КодВНД}-Р{Number}, например "10062-Р3"</summary>
    public required string Code { get; set; }

    // --- Основные файлы редакции (DOC/DOCX)
    public int DocFileRuId { get; set; }
    public FileAttachment? DocFileRu { get; set; }

    public int? DocFileKgId { get; set; }
    public FileAttachment? DocFileKg { get; set; }

    public int? DocFileEnId { get; set; }
    public FileAttachment? DocFileEn { get; set; }

    /// <summary>Таблица изменений и дополнений (ТИД) — Word-файл, обязателен, если у ВНД уже была
    /// предыдущая редакция (Number > 1, то есть документ актуализируется, а не создаётся впервые).
    /// При повторной отправке после замечаний (ResubmitAfterRevisionAsync) обновляется тем же файлом
    /// или новым, если инициатор его заменил.</summary>
    public int? TidFileId { get; set; }
    public FileAttachment? TidFile { get; set; }

    // --- Согласование
    public bool RequiresApproval { get; set; }
    public RedactionApprovalStatus ApprovalStatus { get; set; } = RedactionApprovalStatus.NotRequired;

    // --- Прочие вложения (Word/Excel/презентации и др.)
    public ICollection<VndRedactionAttachment> Attachments { get; set; } = new List<VndRedactionAttachment>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}