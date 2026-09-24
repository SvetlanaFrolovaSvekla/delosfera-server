namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class AddVndLinkRequest
{
    public required int TargetVndId { get; set; }

    /// <summary>Место в тексте ЭТОГО документа, где упоминается ссылка ("Добавить ссылку" →
    /// "С упоминанием в тексте"). null - "Без упоминания в тексте" (общая ссылка документа).</summary>
    public VndLinkAnchorRequest? Source { get; set; }

    /// <summary>Место в тексте ЦЕЛЕВОГО документа, на которое ведёт ссылка. null - на весь документ.</summary>
    public VndLinkAnchorRequest? Target { get; set; }
}

/// <summary>"Якорь" фрагмента текста редакции - тот же принцип, что и у цитат согласующих
/// (см. VndApprovalStageQuote / quoteAnchor.ts на клиенте): сам фрагмент + немного текста до и
/// после + номер вхождения, чтобы найти ИМЕННО это место, даже если фраза встречается в
/// документе несколько раз.</summary>
public class VndLinkAnchorRequest
{
    public required int RedactionId { get; set; }

    /// <summary>"ru" / "kg" / "en"</summary>
    public required string DocumentTarget { get; set; }

    public required string Text { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public int? Occurrence { get; set; }
}
