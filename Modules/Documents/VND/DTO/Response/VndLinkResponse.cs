namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndLinkResponse
{
    public int Id { get; set; }
    public int VndId { get; set; } // id документа на другом конце связи
    public required string Code { get; set; }
    public required string Title { get; set; }
    public required string Status { get; set; } // "active" | "onact" | "review" | "consol" | "arch" | "draft"

    /// <summary>"manual" - добавлена вручную через "Добавить ссылку"; "legacy" - гиперссылка
    /// db://documents/{код} из текста Word-файла редакции (isrib), проиндексированная
    /// автоматически (см. VndLegacyLinkIndexer).</summary>
    public string Kind { get; set; } = "manual";

    /// <summary>То же, что Kind == "legacy" (оставлено для обратной совместимости): такую связь
    /// нельзя удалить - она исчезнет сама, когда гиперссылку уберут из текста редакции.</summary>
    public bool IsAutoDetected { get; set; }

    /// <summary>Где ссылка упоминается в тексте ССЫЛАЮЩЕГОСЯ документа. null - "без упоминания
    /// в тексте" (общая ссылка документа).</summary>
    public VndLinkAnchorResponse? Source { get; set; }

    /// <summary>На какое место документа, НА КОТОРЫЙ ссылаются, ведёт ссылка. null - на весь
    /// документ.</summary>
    public VndLinkAnchorResponse? Target { get; set; }

    public DateTime? CreatedAt { get; set; }
}

/// <summary>Место в тексте конкретной редакции документа (см. VndLink.Source*/Target*).</summary>
public class VndLinkAnchorResponse
{
    public int RedactionId { get; set; }
    public int RedactionNumber { get; set; }
    public required string RedactionCode { get; set; }
    public bool IsCurrentRedaction { get; set; }

    /// <summary>См. RedactionApprovalStatus: "NotRequired" | "Draft" | "Pending" | "Approved" | "Rejected"</summary>
    public required string RedactionApprovalStatus { get; set; }

    /// <summary>"ru" / "kg" / "en"</summary>
    public string? DocumentTarget { get; set; }

    public string? Text { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public int? Occurrence { get; set; }

    /// <summary>Только для легаси-ссылки: код из гиперссылки db://documents/{код}.</summary>
    public string? LegacyCode { get; set; }
}

/// <summary>Ссылка на вложение ЭТОГО ЖЕ документа, обнаруженная в тексте текущей редакции
/// (легаси-гиперссылка db://attachments/{n} из isrib) - в отличие от VndLinkResponse, это не
/// связь с другим ВНД, а ссылка на файл, приложенный к текущей редакции этого документа, которая
/// в браузере не открывает ничего сама по себе - см. DocxLegacyLinkExtractor.</summary>
public class VndAttachmentLinkResponse
{
    /// <summary>Легаси-номер вложения из ссылки (db://attachments/{legacyIndex}), 1-based.</summary>
    public required int LegacyIndex { get; set; }
    public int FileId { get; set; }
    public required string FileName { get; set; }

    /// <summary>false - у редакции нет вложения с таким порядковым номером (например,
    /// вложение было позже удалено, либо номер относился к вложениям другой редакции при
    /// миграции из isrib) - на фронте показываем такую ссылку как нерабочую, а не кликабельную.</summary>
    public bool Resolved { get; set; }

    // --- В тексте какой редакции найдена ссылка (номер вложения относится к вложениям именно
    // этой редакции).
    public int RedactionId { get; set; }
    public int RedactionNumber { get; set; }
    public string RedactionCode { get; set; } = "";
    public bool IsCurrentRedaction { get; set; }
    public string RedactionApprovalStatus { get; set; } = "";

    /// <summary>В тексте на каких языках ("ru"/"kg"/"en") встречается ссылка.</summary>
    public List<string> Languages { get; set; } = [];
}

public class VndLinksResponse
{
    /// <summary>Ссылки на другие документы (этот документ - источник) - как добавленные вручную,
    /// так и автоматически обнаруженные в тексте (см. VndLinkResponse.IsAutoDetected).</summary>
    public List<VndLinkResponse> Outgoing { get; set; } = [];

    /// <summary>Документы, ссылающиеся на этот (этот документ - цель)</summary>
    public List<VndLinkResponse> Incoming { get; set; } = [];

    /// <summary>Ссылки на собственные вложения этого документа, обнаруженные в тексте его
    /// редакций (db://attachments/{n}) - по одной записи на (редакция, номер вложения), см.
    /// VndRedaction.LegacyAttachmentRefs.</summary>
    public List<VndAttachmentLinkResponse> AttachmentReferences { get; set; } = [];
}