namespace delosfera_server.Modules.Dictionaries.DTO.Response;

public class TypeVndResponse
{
    public int Id { get; set; }
    /// <summary>Название, разрешённое под язык текущего запроса</summary>
    public required string Name { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}