namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Новое предложение по ВНД (multipart/form-data) — см. VndProposal.</summary>
public class CreateVndProposalRequest
{
    public string? Text { get; set; }

    /// <summary>Редакция, которую смотрел автор (и из которой взяты цитаты). Необязательно.</summary>
    public int? RedactionId { get; set; }

    /// <summary>Цитаты из текста редакции — JSON-массив <see cref="VndProposalQuoteItem"/>. Одним
    /// form-полем по той же причине, что и ApprovalDecisionRequest.QuotesJson: комплексный биндинг
    /// списков через [FromForm] в ASP.NET Core ненадёжен.</summary>
    public string? QuotesJson { get; set; }

    /// <summary>Файлы — контроллер забирает их напрямую из Request.Form.Files (см. VndApprovalController.Decide).</summary>
    public List<IFormFile>? Files { get; set; }
}

public class VndProposalQuoteItem
{
    public string DocumentTarget { get; set; } = "ru";
    public string Text { get; set; } = "";
    public string? Note { get; set; }
}

/// <summary>Фильтр списка предложений (страница "Предложения по ВНД").</summary>
public class VndProposalFilterRequest
{
    /// <summary>"all" (по умолчанию) / "unread" / "read".</summary>
    public string? Status { get; set; }

    /// <summary>Поиск по коду/названию ВНД, тексту предложения, цитатам и ФИО автора.</summary>
    public string? Search { get; set; }

    public int? VndId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
