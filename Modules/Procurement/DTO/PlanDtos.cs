using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Позиция Плана закупок с фактом исполнения (PRC-22).</summary>
public class PlanItemDto
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Subject { get; set; }
    public decimal PlannedAmount { get; set; }
    public int? Quarter { get; set; }
    public string? OrgUnitTitle { get; set; }
    public required string SubjectKindTitle { get; set; }
    public string? Note { get; set; }

    /// <summary>Сколько заявок сослалось на позицию и на какую сумму.</summary>
    public int RequestCount { get; set; }
    public decimal ActualAmount { get; set; }

    /// <summary>Отклонение факта от плана в процентах; null — заявок не было.</summary>
    public decimal? DeviationPercent { get; set; }

    /// <summary>Факт превысил плановую сумму — повод для корректировки плана.</summary>
    public bool IsOverrun { get; set; }
}

/// <summary>Годовой План закупок.</summary>
public class PlanDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public PlanStatus Status { get; set; }
    public required string StatusTitle { get; set; }
    public string? ApprovalProtocol { get; set; }
    public DateOnly? ApprovedOn { get; set; }

    public List<PlanItemDto> Items { get; set; } = [];

    public decimal PlannedTotal { get; set; }
    public decimal ActualTotal { get; set; }

    /// <summary>Закупки года, не привязанные к плану (PRC-03).</summary>
    public int UnplannedRequestCount { get; set; }
    public decimal UnplannedAmount { get; set; }
}

public class PlanCreateRequest
{
    public int Year { get; set; }
}

public class PlanItemRequest
{
    public required string Code { get; set; }
    public required string Subject { get; set; }
    public decimal PlannedAmount { get; set; }
    public int? Quarter { get; set; }
    public int? OrgUnitId { get; set; }
    public ProcurementSubjectKind SubjectKind { get; set; } = ProcurementSubjectKind.Goods;
    public string? Note { get; set; }
}

public class PlanApproveRequest
{
    public required string ApprovalProtocol { get; set; }
    public DateOnly? ApprovedOn { get; set; }
}
