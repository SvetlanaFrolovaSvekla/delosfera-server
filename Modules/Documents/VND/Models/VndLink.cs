namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Связь "документ ссылается на документ".
///
/// Бывает двух видов (см. <see cref="Kind"/>):
/// - <see cref="VndLinkKind.Manual"/> - добавлена пользователем кнопкой "Добавить ссылку".
///   Может быть "без упоминания в тексте" (SourceRedactionId == null - общая ссылка документа,
///   как было раньше) или "с упоминанием в тексте" - тогда она привязана к конкретному фрагменту
///   текста конкретной редакции (SourceRedactionId + SourceDocumentTarget + якорь SourceText/
///   SourcePrefix/SourceSuffix/SourceOccurrence - тот же принцип "якоря", что и у цитат
///   согласующих, см. <see cref="VndApprovalStageQuote"/> и quoteAnchor.ts на клиенте).
/// - <see cref="VndLinkKind.LegacyText"/> - гиперссылка db://documents/{код}, прошитая в сам
///   Word-файл редакции ещё в старой системе (isrib). Такие записи не создаются вручную, а
///   индексируются автоматически из файлов КАЖДОЙ редакции (см. VndLegacyLinkIndexer) - по одной
///   строке на пару (редакция, язык, код) - чтобы на вкладке "Связанные документы" было видно,
///   какая именно редакция ссылается, и чтобы у документа-цели они появлялись в "Ссылающихся".
///
/// Цель ссылки - либо весь документ (TargetRedactionId == null), либо конкретное место в тексте
/// конкретной редакции целевого документа (TargetRedactionId + TargetDocumentTarget + якорь).</summary>
public class VndLink
{
    public int Id { get; set; }
    public int SourceVndId { get; set; } // Документ, который ссылается
    public VndDocument? SourceVnd { get; set; }
    public int TargetVndId { get; set; } // Документ, на который ссылаются
    public VndDocument? TargetVnd { get; set; }

    public VndLinkKind Kind { get; set; } = VndLinkKind.Manual;

    // --- Где ссылка упоминается в тексте ИСХОДНОГО документа (null - "без упоминания в тексте")

    public int? SourceRedactionId { get; set; }
    public VndRedaction? SourceRedaction { get; set; }

    /// <summary>"ru" / "kg" / "en" - в тексте на каком языке находится упоминание.</summary>
    public string? SourceDocumentTarget { get; set; }

    /// <summary>Выделенный фрагмент текста, к которому прикреплена ссылка (для LegacyText -
    /// не заполняется, место находится по самой гиперссылке, см. LegacyCode).</summary>
    public string? SourceText { get; set; }
    public string? SourcePrefix { get; set; }
    public string? SourceSuffix { get; set; }
    public int? SourceOccurrence { get; set; }

    /// <summary>Только для LegacyText: код документа из гиперссылки db://documents/{код} (без
    /// ведущих нулей) - по нему клиент находит саму гиперссылку в отрендеренном тексте.</summary>
    public string? LegacyCode { get; set; }

    // --- На какое место ЦЕЛЕВОГО документа ведёт ссылка (null - "на весь документ")

    public int? TargetRedactionId { get; set; }
    public VndRedaction? TargetRedaction { get; set; }
    public string? TargetDocumentTarget { get; set; }
    public string? TargetText { get; set; }
    public string? TargetPrefix { get; set; }
    public string? TargetSuffix { get; set; }
    public int? TargetOccurrence { get; set; }

    public int? CreatedByUserId { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public enum VndLinkKind
{
    /// <summary>Добавлена вручную ("Добавить ссылку").</summary>
    Manual = 0,

    /// <summary>Легаси-гиперссылка db://documents/{код} из текста Word-файла редакции (isrib).</summary>
    LegacyText = 1,
}
