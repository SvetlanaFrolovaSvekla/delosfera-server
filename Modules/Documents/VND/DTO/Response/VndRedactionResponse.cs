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

    /// <summary>Лист согласования — формируется автоматически, когда согласование редакции
    /// окончательно завершается. Null, пока редакция не согласована.</summary>
    public int? ApprovalSheetFileId { get; set; }

    public bool RequiresApproval { get; set; }
    public required string ApprovalStatus { get; set; }

    /// <summary>Оставлено для обратной совместимости — те же id, что и FileId в Attachments.
    /// Новый код должен использовать Attachments (там же есть настоящее имя файла).</summary>
    public List<int> AttachmentFileIds { get; set; } = [];

    /// <summary>Прочие вложения редакции — с оригинальным именем файла (как при загрузке),
    /// чтобы показывать и скачивать его под тем же именем, а не "Вложение #id".</summary>
    public List<VndRedactionAttachmentResponse> Attachments { get; set; } = [];

    // --- Реквизиты ИМЕННО этой редакции (см. VndRedaction — переходный период миграции
    // "реквизиты по редакции"). Используются, в частности, для вкладок Р1/Р2/Р3... на
    // вкладке "Реквизиты" и для сравнения "что изменилось по сравнению с предыдущей редакцией".
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public int TypeId { get; set; }
    public required string TypeName { get; set; }

    public DateOnly? AdoptionDate { get; set; }
    public string? AdoptionCode { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public required string Period { get; set; }

    public int DeveloperId { get; set; }
    public required string DeveloperName { get; set; }
    public int? CuratorDeveloperId { get; set; }
    public string? CuratorDeveloperName { get; set; }

    public int OrganId { get; set; }
    public required string OrganName { get; set; }

    public int SecrecyLevelId { get; set; }

    public List<int> ResponsibleExecutorIds { get; set; } = [];
    public List<int> KeywordIds { get; set; } = [];
    public List<int> RubricIds { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}

public class VndRedactionAttachmentResponse
{
    public int FileId { get; set; }
    public required string FileName { get; set; }
    public long SizeBytes { get; set; }
}