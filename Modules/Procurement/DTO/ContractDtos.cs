using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Акт приёма-передачи в карточке договора (PRC-19).</summary>
public class DeliveryActDto
{
    public int Id { get; set; }
    public required string Number { get; set; }
    public DateOnly ActDate { get; set; }
    public decimal Amount { get; set; }
    public string? Subject { get; set; }

    public string? ApprovedByUnitHead { get; set; }
    public DateTime? UnitHeadApprovedAt { get; set; }

    public string? ApprovedByCurator { get; set; }
    public DateTime? CuratorApprovedAt { get; set; }

    /// <summary>Сумма выше порога — нужна ещё виза курирующего члена Правления.</summary>
    public bool RequiresCuratorApproval { get; set; }

    /// <summary>Акт принят: собраны все требуемые утверждения.</summary>
    public bool IsApproved { get; set; }
}

/// <summary>Карточка договора закупки (PRC-18/19).</summary>
public class ContractDto
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? RegNumber { get; set; }

    public int RequestId { get; set; }
    public string? RequestRegNumber { get; set; }
    public required string Subject { get; set; }

    public int? ProtocolId { get; set; }
    public string? ProtocolRegNumber { get; set; }
    public int? TenderId { get; set; }
    public string? TenderRegNumber { get; set; }

    public required string SupplierTitle { get; set; }
    public string? SupplierInn { get; set; }

    public ContractStatus Status { get; set; }
    public required string StatusTitle { get; set; }

    public decimal Amount { get; set; }
    public DateOnly? SignedOn { get; set; }
    public DateOnly? DeliveryDeadline { get; set; }
    public DateOnly? PaymentDeadline { get; set; }

    public string? ResponsibleName { get; set; }

    /// <summary>Кто готовит договор по правилу PRC-18 — пояснение для карточки.</summary>
    public required string ResponsibleRule { get; set; }

    public string? TerminationReason { get; set; }

    public List<DeliveryActDto> Acts { get; set; } = [];

    /// <summary>Принято по актам — сколько от суммы договора закрыто.</summary>
    public decimal AcceptedAmount { get; set; }

    /// <summary>Срок поставки истёк, а акты не закрывают сумму договора.</summary>
    public bool IsDeliveryOverdue { get; set; }

    public List<string> Blockers { get; set; } = [];
}

public class ContractCreateRequest
{
    /// <summary>Поставщик; пусто — берётся победитель протокола или конкурса.</summary>
    public int? SupplierId { get; set; }

    public decimal? Amount { get; set; }
    public DateOnly? SignedOn { get; set; }
    public DateOnly? DeliveryDeadline { get; set; }
    public DateOnly? PaymentDeadline { get; set; }
    public int? ResponsibleUserId { get; set; }
}

public class ContractUpdateRequest
{
    public DateOnly? SignedOn { get; set; }
    public DateOnly? DeliveryDeadline { get; set; }
    public DateOnly? PaymentDeadline { get; set; }
    public int? ResponsibleUserId { get; set; }
}

public class DeliveryActRequest
{
    public required string Number { get; set; }
    public DateOnly? ActDate { get; set; }
    public decimal Amount { get; set; }
    public string? Subject { get; set; }
}

public class ContractTerminateRequest
{
    public required string Reason { get; set; }
}
