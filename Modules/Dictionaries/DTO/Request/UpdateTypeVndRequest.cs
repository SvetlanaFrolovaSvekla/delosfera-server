namespace delosfera_server.Modules.Dictionaries.DTO.Request;

public class UpdateTypeVndRequest
{
    /// <summary>Название на русском (обязательное поле для заполнения)</summary>
    public required string TitleRu { get; set; }

    /// <summary>Название на английском</summary>
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском</summary>
    public string? TitleKg { get; set; }
}