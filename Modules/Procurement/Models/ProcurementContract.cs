using delosfera_server.Common.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>Стадия договора закупки (PRC-18/19).</summary>
public enum ContractStatus
{
    Draft = 1,

    /// <summary>Подписан сторонами, идёт поставка или оказание услуг.</summary>
    Active = 2,

    /// <summary>Обязательства исполнены, акты приняты.</summary>
    Completed = 3,

    /// <summary>Расторгнут с указанием основания.</summary>
    Terminated = 4,
}

/// <summary>
/// Договор по итогам закупки (PRC-18/19). Связан с заявкой, протоколом и решением,
/// по которому заключён: без этой связи невозможно показать, на каком основании
/// возникло обязательство.
///
/// Кто готовит договор, определяется типом предмета: товар — Сектор закупок,
/// товар с установкой и итоги конкурса — инициатор (PRC-18).
/// </summary>
public class ProcurementContract : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Единая карточка документа: номер, статус, автор, вложения, аудит.</summary>
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int RequestId { get; set; }
    public ProcurementRequest? Request { get; set; }

    /// <summary>Протокол закупки, по которому заключён договор.</summary>
    public int? ProtocolId { get; set; }
    public ProcurementProtocol? Protocol { get; set; }

    /// <summary>Конкурс, если договор заключён по его итогам.</summary>
    public int? TenderId { get; set; }
    public Tender? Tender { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    /// <summary>Сумма договора с учётом дополнительно приобретённого количества.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Стоимость договора при заключении.
    ///
    /// От неё считается право на дополнительное количество: Банк вправе
    /// приобрести у поставщика ещё не более двадцати пяти процентов стоимости
    /// договора, заключённого по результатам конкурса (п. 6/7 раздела VIII).
    /// Без отдельного поля предел пришлось бы считать от уже увеличенной суммы,
    /// и каждая допоставка расширяла бы право на следующую.
    /// </summary>
    public decimal InitialAmount { get; set; }

    /// <summary>
    /// Служебная записка, которой согласовано дополнительное количество.
    /// Положение требует согласовать её с куратором инициатора, организатором
    /// и куратором организатора — без записки допоставки не бывает.
    /// </summary>
    public int? TopUpSzId { get; set; }

    public DateOnly? SignedOn { get; set; }

    /// <summary>Срок поставки товара или оказания услуг по договору.</summary>
    public DateOnly? DeliveryDeadline { get; set; }

    /// <summary>Срок оплаты — контролируется вместе со сроком поставки (PRC-19).</summary>
    public DateOnly? PaymentDeadline { get; set; }

    /// <summary>
    /// Ответственный за подготовку и заключение: Сектор закупок либо инициатор.
    /// Правило зависит от типа предмета и способа закупки (PRC-18).
    /// </summary>
    public int? ResponsibleUserId { get; set; }
    public User? ResponsibleUser { get; set; }

    public string? TerminationReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<DeliveryAct> Acts { get; set; } = new List<DeliveryAct>();
}

/// <summary>
/// Акт приёма-передачи или выполненных работ (PRC-19). Утверждается начальником
/// инициирующего СП, а свыше порога — дополнительно курирующим членом Правления.
/// </summary>
public class DeliveryAct
{
    public int Id { get; set; }

    public int ContractId { get; set; }
    public ProcurementContract? Contract { get; set; }

    public required string Number { get; set; }

    public DateOnly ActDate { get; set; }

    public decimal Amount { get; set; }

    public string? Subject { get; set; }

    /// <summary>Утверждение начальником инициирующего СП.</summary>
    public int? ApprovedByUnitHeadId { get; set; }
    public User? ApprovedByUnitHead { get; set; }
    public DateTime? UnitHeadApprovedAt { get; set; }

    /// <summary>
    /// Утверждение курирующим членом Правления. Требуется, когда сумма акта
    /// превышает установленный порог (PRC-19).
    /// </summary>
    public int? ApprovedByCuratorId { get; set; }
    public User? ApprovedByCurator { get; set; }
    public DateTime? CuratorApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
