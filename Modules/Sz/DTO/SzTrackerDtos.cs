namespace delosfera_server.Modules.Sz.DTO;

/// <summary>Колонка доски записок (РС-4): стадия и записки на ней.</summary>
public class SzTrackerColumnDto
{
    public required string Code { get; set; }
    public required string Title { get; set; }
    public int Count { get; set; }
    public List<SzTrackerItemDto> Items { get; set; } = [];
}

/// <summary>Записка на доске.</summary>
public class SzTrackerItemDto
{
    public int Id { get; set; }
    public string? RegNumber { get; set; }
    public required string Title { get; set; }
    public required string Kind { get; set; }
    public string? AuthorName { get; set; }
    public string? AddresseeName { get; set; }

    /// <summary>Сколько дней записка живёт (от создания).</summary>
    public int AgeDays { get; set; }

    /// <summary>Активная записка висит дольше норматива — повод разобраться.</summary>
    public bool IsStale { get; set; }
}
