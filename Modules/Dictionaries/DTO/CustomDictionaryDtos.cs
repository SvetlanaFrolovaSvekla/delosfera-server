namespace delosfera_server.Modules.Dictionaries.DTO;

public class CustomDictionarySaveRequest
{
    /// <summary>Системное имя справочника; при изменении не меняется.</summary>
    public string? Code { get; set; }

    public string? TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public string? Description { get; set; }
    public bool IsHierarchical { get; set; }
    public bool? IsActive { get; set; }
}

public class CustomDictionaryItemRequest
{
    public string? TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public string? Code { get; set; }
    public int? ParentId { get; set; }
    public int? Order { get; set; }
    public bool? IsActive { get; set; }
}

public class CustomDictionaryItemDto
{
    public int Id { get; set; }
    public string TitleRu { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public string? Code { get; set; }
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
}

public class CustomDictionaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TitleRu { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public string? Description { get; set; }
    public bool IsHierarchical { get; set; }
    public bool IsActive { get; set; }
    public List<CustomDictionaryItemDto> Items { get; set; } = [];
}
