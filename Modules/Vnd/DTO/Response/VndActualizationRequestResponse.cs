namespace delosfera_server.Modules.Vnd.DTO.Response;

public class VndActualizationRequestResponse
{
    public int Id { get; set; }
    public int VndId { get; set; }
    public required string VndCode { get; set; }
    public required string VndTitle { get; set; }

    public int RequestedByUserId { get; set; }
    public required string RequestedByName { get; set; }

    public bool RequiresApproval { get; set; }
    public required string Status { get; set; } // "pending" | "approved" | "rejected"

    public int? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}