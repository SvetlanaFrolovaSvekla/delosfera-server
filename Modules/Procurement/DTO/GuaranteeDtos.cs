using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Гарантийное обеспечение (PRC-20).</summary>
public class GuaranteeDto
{
    public int Id { get; set; }

    public GuaranteeKind Kind { get; set; }
    public required string KindTitle { get; set; }
    public GuaranteeForm Form { get; set; }
    public required string FormTitle { get; set; }

    public int? TenderId { get; set; }
    public string? TenderRegNumber { get; set; }
    public int? ContractId { get; set; }
    public string? ContractRegNumber { get; set; }

    public int SupplierId { get; set; }
    public required string SupplierTitle { get; set; }

    public decimal Amount { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string? DocumentRef { get; set; }

    public DateOnly? ReturnedOn { get; set; }
    public string? ReturnedBy { get; set; }
    public bool IsForfeited { get; set; }
    public string? Note { get; set; }

    /// <summary>Срок действия истёк, а обеспечение не возвращено и не удержано.</summary>
    public bool IsReturnOverdue { get; set; }

    /// <summary>Сколько дней осталось до окончания срока; отрицательное — просрочено.</summary>
    public int DaysLeft { get; set; }
}

public class GuaranteeCreateRequest
{
    public GuaranteeKind Kind { get; set; }
    public GuaranteeForm Form { get; set; } = GuaranteeForm.Cash;

    public int? TenderId { get; set; }
    public int? ContractId { get; set; }

    public int? SupplierId { get; set; }
    public string? SupplierTitle { get; set; }
    public string? SupplierInn { get; set; }

    public decimal Amount { get; set; }
    public DateOnly? ReceivedOn { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string? DocumentRef { get; set; }
}

public class GuaranteeReturnRequest
{
    /// <summary>Обеспечение удержано, а не возвращено — основание обязательно.</summary>
    public bool Forfeit { get; set; }
    public string? Note { get; set; }
}

/// <summary>Претензия по договору (PRC-21).</summary>
public class ClaimDto
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string? ContractRegNumber { get; set; }
    public string? SupplierTitle { get; set; }

    public string? RegNumber { get; set; }
    public ClaimStatus Status { get; set; }
    public required string StatusTitle { get; set; }

    public required string Violation { get; set; }
    public string? Demand { get; set; }
    public decimal? Amount { get; set; }

    public DateOnly? SentOn { get; set; }
    public DateOnly? ResponseDeadline { get; set; }
    public DateOnly? AnsweredOn { get; set; }
    public string? Response { get; set; }
    public string? Outcome { get; set; }

    /// <summary>Срок ответа истёк, а контрагент не ответил.</summary>
    public bool IsResponseOverdue { get; set; }
}

public class ClaimCreateRequest
{
    public required string Violation { get; set; }
    public string? Demand { get; set; }
    public decimal? Amount { get; set; }
}

public class ClaimSendRequest
{
    public DateOnly? SentOn { get; set; }

    /// <summary>Срок ответа контрагента; пусто — 30 календарных дней от направления.</summary>
    public DateOnly? ResponseDeadline { get; set; }
}

public class ClaimAnswerRequest
{
    public required string Response { get; set; }
    public DateOnly? AnsweredOn { get; set; }
}

public class ClaimCloseRequest
{
    public ClaimStatus Status { get; set; }
    public required string Outcome { get; set; }
}
