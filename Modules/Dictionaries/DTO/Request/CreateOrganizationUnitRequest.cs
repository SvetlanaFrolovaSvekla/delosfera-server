namespace delosfera_server.Modules.Dictionaries.DTO.Request;

/// <summary>
/// Данные для создания нового структурного подразделения
/// </summary>
public class CreateOrganizationUnitRequest
{
    /// <summary>Название на русском (обязательно)</summary>
    public required string TitleRu { get; set; }

    /// <summary>Название на английском (опционально)</summary>
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском (опционально)</summary>
    public string? TitleKg { get; set; }

    /// <summary>Идентификатор родительского подразделения (если есть)</summary>
    public int? ParentId { get; set; }
    
    public int? HeadUserId { get; set; }
    
    public int? CuratorUserId { get; set; }
}