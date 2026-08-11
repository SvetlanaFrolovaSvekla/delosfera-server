using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Dictionaries.DTO.Request;

public class UpdateTypeVndRequest
{
    /// <summary>Название на русском (обязательное поле для заполнения)</summary>
    [Required, StringLength(300, MinimumLength = 1)]
    public required string TitleRu { get; set; }

    /// <summary>Название на английском</summary>
    [StringLength(300)]
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском</summary>
    [StringLength(300)]
    public string? TitleKg { get; set; }
}