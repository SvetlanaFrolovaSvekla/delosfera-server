namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>
/// Пакет публикации объявления о конкурсе (INT-05): готовый текст и реквизиты
/// для сайта Банка и procurement.kg.
/// </summary>
public class PublicationPackageDto
{
    public int TenderId { get; set; }
    public string? TenderRegNumber { get; set; }

    public required string Subject { get; set; }
    public required string MethodTitle { get; set; }
    public decimal Amount { get; set; }
    public string? InitiatorUnit { get; set; }

    public DateOnly? PublishedOn { get; set; }
    public DateOnly? SubmissionDeadline { get; set; }

    /// <summary>Конкурс с ограниченным участием — объявление не публикуется.</summary>
    public bool IsLimited { get; set; }

    /// <summary>Куда размещается объявление.</summary>
    public List<string> Channels { get; set; } = [];

    /// <summary>Текст объявления, готовый к размещению.</summary>
    public required string Announcement { get; set; }

    /// <summary>Чего не хватает для публикации.</summary>
    public List<string> Blockers { get; set; } = [];
}
