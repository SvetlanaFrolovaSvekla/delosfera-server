namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndActualizationRequestResponse
{
    public int Id { get; set; }
    public int VndId { get; set; }
    public required string VndCode { get; set; }
    public required string VndTitle { get; set; }

    public int RequestedByUserId { get; set; }
    public required string RequestedByName { get; set; }

    public bool RequiresApproval { get; set; }

    /// <summary>Пожелание заявителя о сдвиге срока — после одобрения может быть заменено на
    /// финальное значение, скорректированное главным редактором (см. DecideRequestAsync).</summary>
    public bool ShiftNextPeriod { get; set; }
    public required string Status { get; set; } // "pending" | "approved" | "rejected"

    public int? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>Момент, когда одобренная заявка была фактически использована для старта цикла
    /// актуализации. Null, пока заявка ещё не одобрена, либо одобрена, но не использована.</summary>
    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}