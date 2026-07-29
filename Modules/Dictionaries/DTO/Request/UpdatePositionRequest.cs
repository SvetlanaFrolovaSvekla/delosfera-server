namespace delosfera_server.Modules.Dictionaries.DTO.Request;

/// <summary>
/// Данные для обновления должности
/// </summary>
public class UpdatePositionRequest
{
    /// <summary>Название на русском (обязательно)</summary>
    public required string TitleRu { get; set; }

    /// <summary>Название на английском (опционально)</summary>
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском (опционально)</summary>
    public string? TitleKg { get; set; }
}