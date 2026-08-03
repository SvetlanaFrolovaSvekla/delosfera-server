namespace delosfera_server.Modules.Vnd.DTO.Request;

public class AddDisagreementMatrixRowRequest
{
    public required string DeveloperPosition { get; set; }
    public required string OpponentPosition { get; set; }
    public string? DeveloperJustification { get; set; }
}