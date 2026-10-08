namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Мягко удалённая ВНД (вкладка «Черновики» → «Удалённые», право DeleteAnyVnd).</summary>
public class DeletedVndResponse
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Статус на момент удаления: "active" / "onact" / "review" / "consol" / "arch" / "draft".</summary>
    public required string Status { get; set; }

    public DateTime? DeletedAt { get; set; }
    public string? DeletedByName { get; set; }
}
