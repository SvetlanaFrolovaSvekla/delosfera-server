namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndLinkResponse
{
    public int Id { get; set; }
    public int VndId { get; set; } // id документа на другом конце связи
    public required string Code { get; set; }
    public required string Title { get; set; }
    public required string Status { get; set; } // "active" | "onact" | "review" | "consol" | "arch" | "draft"

    /// <summary>true - ссылка не добавлена вручную через "Добавить ссылку", а автоматически
    /// обнаружена в тексте текущей редакции (легаси-гиперссылка db://documents/{код},
    /// унаследованная из старой системы isrib - см. DocxLegacyLinkExtractor и
    /// VndService.GetLinksAsync). Такая запись не хранится в vnd_link, поэтому Id у неё = 0
    /// и её нельзя удалить через DeleteLinkAsync — на фронте для неё не показывается кнопка
    /// удаления.</summary>
    public bool IsAutoDetected { get; set; }
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

    /// <summary>false - у текущей редакции нет вложения с таким порядковым номером (например,
    /// вложение было позже удалено, либо номер относился к вложениям другой редакции при
    /// миграции из isrib) - на фронте показываем такую ссылку как нерабочую, а не кликабельную.</summary>
    public bool Resolved { get; set; }
}

public class VndLinksResponse
{
    /// <summary>Ссылки на другие документы (этот документ - источник) - как добавленные вручную,
    /// так и автоматически обнаруженные в тексте (см. VndLinkResponse.IsAutoDetected).</summary>
    public List<VndLinkResponse> Outgoing { get; set; } = [];

    /// <summary>Документы, ссылающиеся на этот (этот документ - цель)</summary>
    public List<VndLinkResponse> Incoming { get; set; } = [];

    /// <summary>Ссылки на собственные вложения этого документа, обнаруженные в тексте текущей
    /// редакции (db://attachments/{n}) - см. DocxLegacyLinkExtractor.</summary>
    public List<VndAttachmentLinkResponse> AttachmentReferences { get; set; } = [];
}