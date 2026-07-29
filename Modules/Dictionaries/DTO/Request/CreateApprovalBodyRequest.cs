namespace delosfera_server.Modules.Dictionaries.DTO.Request;

/// <summary>
/// Данные для создания нового органа утверждения
/// </summary>
public class CreateApprovalBodyRequest
{
    /// <summary>Название на русском (обязательно)</summary>
    public required string TitleRu { get; set; }

    /// <summary>Название на английском (опционально)</summary>
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском (опционально)</summary>
    public string? TitleKg { get; set; }

    /// <summary>Идентификатор родительского органа утверждения (если есть)</summary>
    public int? ParentId { get; set; }
}