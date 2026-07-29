namespace delosfera_server.Modules.Dictionaries.DTO.Request;

/// <summary>
/// Данные для обновления группы пользователей
/// </summary>
public class UpdateUserGroupRequest
{
    /// <summary>Название на русском (обязательно)</summary>
    public required string TitleRu { get; set; }

    /// <summary>Название на английском (опционально)</summary>
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском (опционально)</summary>
    public string? TitleKg { get; set; }

    /// <summary>Идентификаторы пользователей, входящих в группу</summary>
    public List<int> UserIds { get; set; } = [];
}