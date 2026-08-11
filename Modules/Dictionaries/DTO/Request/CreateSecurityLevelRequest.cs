using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Dictionaries.DTO.Request;

/// <summary>
/// Данные для создания нового уровня секретности
/// </summary>
public class CreateSecurityLevelRequest
{
    /// <summary>Название на русском (обязательно)</summary>
    [Required, StringLength(300, MinimumLength = 1)]
    public required string TitleRu { get; set; }

    /// <summary>Название на английском (опционально)</summary>
    [StringLength(300)]
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском (опционально)</summary>
    [StringLength(300)]
    public string? TitleKg { get; set; }
}