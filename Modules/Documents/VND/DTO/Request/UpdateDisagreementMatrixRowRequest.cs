namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class UpdateDisagreementMatrixRowRequest
{
    public required string DeveloperPosition { get; set; }
    public required string OpponentPosition { get; set; }
    public string? DeveloperJustification { get; set; }
}
