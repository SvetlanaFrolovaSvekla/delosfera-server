using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>Вид гарантийного обеспечения (PRC-20).</summary>
public enum GuaranteeKind
{
    /// <summary>ГОКЗ — гарантийное обеспечение конкурсной заявки.</summary>
    BidSecurity = 1,

    /// <summary>ГОИД — гарантийное обеспечение исполнения договора.</summary>
    PerformanceSecurity = 2,
}

/// <summary>Форма обеспечения.</summary>
public enum GuaranteeForm
{
    /// <summary>Денежные средства на счёте Банка.</summary>
    Cash = 1,

    /// <summary>Банковская гарантия.</summary>
    BankGuarantee = 2,
}

/// <summary>
/// Гарантийное обеспечение конкурсной заявки или исполнения договора (PRC-20).
///
/// Возврат — не отметка «сделано», а дата и основание: обеспечение либо возвращается
/// в срок, либо удерживается, и в обоих случаях должно быть видно, почему.
/// </summary>
public class Guarantee : IAuditableEntity
{
    public int Id { get; set; }

    public GuaranteeKind Kind { get; set; }
    public GuaranteeForm Form { get; set; }

    /// <summary>Конкурс, к заявке которого относится ГОКЗ.</summary>
    public int? TenderId { get; set; }
    public Tender? Tender { get; set; }

    /// <summary>Договор, исполнение которого обеспечивается ГОИД.</summary>
    public int? ContractId { get; set; }
    public ProcurementContract? Contract { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public decimal Amount { get; set; }

    public DateOnly ReceivedOn { get; set; }

    /// <summary>До какой даты действует обеспечение — по нему контролируется возврат.</summary>
    public DateOnly ValidUntil { get; set; }

    /// <summary>Реквизиты банковской гарантии или платёжного документа.</summary>
    public string? DocumentRef { get; set; }

    public DateOnly? ReturnedOn { get; set; }
    public int? ReturnedByUserId { get; set; }
    public User? ReturnedBy { get; set; }

    /// <summary>Обеспечение удержано: поставщик нарушил условия участия или договора.</summary>
    public bool IsForfeited { get; set; }

    /// <summary>Основание удержания либо примечание к возврату.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Стадия претензии (PRC-21).</summary>
public enum ClaimStatus
{
    /// <summary>Нарушение зафиксировано, письмо готовится.</summary>
    Draft = 1,

    /// <summary>Претензионное письмо направлено контрагенту.</summary>
    Sent = 2,

    /// <summary>Получен ответ поставщика.</summary>
    Answered = 3,

    /// <summary>Требования удовлетворены.</summary>
    Satisfied = 4,

    /// <summary>Спор передан в суд.</summary>
    Litigation = 5,

    /// <summary>Претензия отозвана.</summary>
    Withdrawn = 6,
}

/// <summary>
/// Претензионная работа по договору (PRC-21): фиксация нарушения, направление
/// письма и контроль ответа контрагента.
///
/// Срок ответа хранится датой, а не «через сколько дней»: он считается от даты
/// направления и должен переживать правку регламента.
/// </summary>
public class ProcurementClaim : IAuditableEntity
{
    public int Id { get; set; }

    public int ContractId { get; set; }
    public ProcurementContract? Contract { get; set; }

    public string? RegNumber { get; set; }

    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

    /// <summary>Что нарушено: срок поставки, качество, комплектность.</summary>
    public required string Violation { get; set; }

    /// <summary>Требование Банка: допоставить, заменить, уплатить неустойку.</summary>
    public string? Demand { get; set; }

    /// <summary>Сумма требования, если оно денежное.</summary>
    public decimal? Amount { get; set; }

    public DateOnly? SentOn { get; set; }

    /// <summary>Срок ответа контрагента — по нему видно просрочку.</summary>
    public DateOnly? ResponseDeadline { get; set; }

    public DateOnly? AnsweredOn { get; set; }
    public string? Response { get; set; }

    /// <summary>Итог работы: чем закончилась претензия.</summary>
    public string? Outcome { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
