namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class DisagreementMatrixRowResponse
{
    public int Id { get; set; }
    public required string DeveloperPosition { get; set; }
    public required string OpponentPosition { get; set; }
    public string? DeveloperJustification { get; set; }
    public DateTime CreatedAt { get; set; }
}