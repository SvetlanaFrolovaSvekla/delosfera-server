namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndQuickSearchResponse
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; } // "active" | "onact" | "review" | "consol" | "arch" | "draft"
}