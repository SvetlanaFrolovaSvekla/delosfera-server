using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class AddDisagreementMatrixRowRequest
{
    [Required, StringLength(2000, MinimumLength = 2)]
    public required string DeveloperPosition { get; set; }

    [Required, StringLength(2000, MinimumLength = 2)]
    public required string OpponentPosition { get; set; }

    [StringLength(2000)]
    public string? DeveloperJustification { get; set; }
}