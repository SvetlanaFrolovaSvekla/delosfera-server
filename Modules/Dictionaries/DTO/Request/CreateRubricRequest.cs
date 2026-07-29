namespace delosfera_server.Modules.Dictionaries.DTO.Request;

/// <summary>
/// Данные для создания новой рубрики
/// </summary>
public class CreateRubricRequest
{
    /// <summary>Название на русском (обязательно)</summary>
    public required string TitleRu { get; set; }

    /// <summary>Название на английском (опционально)</summary>
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском (опционально)</summary>
    public string? TitleKg { get; set; }

    /// <summary>Идентификатор родительской рубрики (если есть)</summary>
    public int? ParentId { get; set; }
}