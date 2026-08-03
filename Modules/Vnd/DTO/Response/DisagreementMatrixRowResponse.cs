namespace delosfera_server.Modules.Vnd.DTO.Response;

public class DisagreementMatrixRowResponse
{
    public int Id { get; set; }
    public required string DeveloperPosition { get; set; }
    public required string OpponentPosition { get; set; }
    public string? DeveloperJustification { get; set; }
    public DateTime CreatedAt { get; set; }
}