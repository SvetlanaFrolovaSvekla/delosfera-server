namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class ApprovalDecisionRequest
{
    public required ApprovalDecisionType Decision { get; set; }

    /// <summary>Обязателен для ApproveWithComment и Reject</summary>
    public string? Comment { get; set; }

    /// <summary>Файлы, приложенные согласующим к своей резолюции (необязательно).
    /// Хранятся бессрочно, наравне с текстом комментария, — остаются частью истории
    /// согласования и после того, как редакция станет согласованной.</summary>
    public List<IFormFile>? Files { get; set; }

    /// <summary>Цитаты из текста редакции, на которые согласующий сослался в Comment (см.
    /// "+ Сослаться на текст редакции" на клиенте) — JSON-массив ApprovalQuoteItem. Передаётся
    /// одним form-полем (а не как список сложных объектов через [FromForm]) по тем же причинам,
    /// по которым Files выше забирается вручную из Request.Form.Files — комплексный биндинг
    /// списков через [FromForm] в ASP.NET Core ненадёжен.</summary>
    public string? QuotesJson { get; set; }
}

/// <summary>Один элемент QuotesJson — см. VndApprovalStageQuote на бэке.</summary>
public class ApprovalQuoteItem
{
    /// <summary>"ru"/"kg"/"en"/"tid"/"approvalSheet"/"disagreementMatrix" — см.
    /// RedactionViewTarget на клиенте.</summary>
    public required string DocumentTarget { get; set; }

    public required string Text { get; set; }
}

public enum ApprovalDecisionType
{
    Approve = 0,
    ApproveWithComment = 1,
    Reject = 2
}