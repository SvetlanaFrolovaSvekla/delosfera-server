using delosfera_server.Common.Models;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>Стадия протокола закупки.</summary>
public enum ProtocolStatus
{
    /// <summary>Сформирован, можно пересобрать и править разделы.</summary>
    Draft = 1,

    /// <summary>Подписан хотя бы одной стороной — пересборка аннулирует подписи.</summary>
    Signing = 2,

    /// <summary>Подписан всеми сторонами и утверждён.</summary>
    Approved = 3,
}

/// <summary>Кто подписывает протокол (по подвалу печатной формы).</summary>
public enum ProtocolSignerRole
{
    /// <summary>Инициатор закупки.</summary>
    Initiator = 1,

    /// <summary>Куратор организатора закупки.</summary>
    OrganizerCurator = 2,

    /// <summary>Утверждающий — Заместитель Председателя Правления, курирующий сектор закупок.</summary>
    Approver = 3,
}

/// <summary>
/// Протокол закупки (PRC-10). Оформляется при сумме свыше настраиваемого порога
/// и фиксирует все поступившие предложения, решение комиссии и основание выбора.
///
/// Протокол — снимок: строки сравнительной таблицы копируются в него на момент
/// формирования. Правка коммерческих предложений после этого не должна менять
/// уже сформированный документ, иначе теряется его доказательная сила.
/// </summary>
public class ProcurementProtocol : IAuditableEntity
{
    public int Id { get; set; }

    public int RequestId { get; set; }
    public ProcurementRequest? Request { get; set; }

    /// <summary>Регистрационный номер протокола.</summary>
    public string? RegNumber { get; set; }

    public DateOnly ProtocolDate { get; set; }

    /// <summary>
    /// Заседание комиссии, итоги которого оформлены этим протоколом.
    ///
    /// «Протокол составляется на каждое заседание комиссии по закупке» — так
    /// сказано в Положении трижды, для обоих видов конкурса и для изучения
    /// заявок. Комиссия собирается не один раз: вскрытие, изучение в течение
    /// десяти рабочих дней, определение победителя. Пока протокол был один на
    /// заявку, каждое следующее заседание затирало предыдущее, и ход обсуждения
    /// восстановить было нельзя.
    ///
    /// Пусто у простой закупки: там комиссия не создаётся и заседаний нет.
    /// </summary>
    public DateOnly? MeetingDate { get; set; }

    public ProtocolStatus Status { get; set; } = ProtocolStatus.Draft;

    // --- снимок заголовка на момент формирования ---

    public required string MethodTitle { get; set; }
    public required string Subject { get; set; }
    public string? InitiatorUnitTitle { get; set; }

    /// <summary>Основной поставщик — победитель отбора.</summary>
    public int? MainSupplierId { get; set; }
    public Supplier? MainSupplier { get; set; }
    public decimal? MainAmount { get; set; }

    /// <summary>
    /// Резервный поставщик — следующее по цене допущенное предложение.
    /// Печатается в решении, чтобы при отказе победителя не проводить закупку заново.
    /// </summary>
    public int? ReserveSupplierId { get; set; }
    public Supplier? ReserveSupplier { get; set; }
    public decimal? ReserveAmount { get; set; }

    // --- разделы протокола по PRC-10 ---

    /// <summary>Виза УПиА о наличии средств в пределах бюджета.</summary>
    public string? BudgetNote { get; set; }

    /// <summary>Оценка эксперта / технического координатора СП.</summary>
    public string? ExpertOpinion { get; set; }

    /// <summary>Особое мнение члена комиссии — фиксируется в протоколе (PRC-15).</summary>
    public string? DissentingOpinion { get; set; }

    /// <summary>Рекомендации по итогам закупки.</summary>
    public string? Recommendations { get; set; }

    /// <summary>
    /// Основание выбора, когда победитель не с наименьшей ценой (PRC-12).
    /// Без него такой протокол не подписывается.
    /// </summary>
    public string? SelectionBasis { get; set; }

    /// <summary>
    /// Хеш содержимого снимка. Подписи ставятся под конкретным содержанием:
    /// пересборка протокола меняет хеш и аннулирует их (SIG-01).
    /// </summary>
    public required string ContentHash { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ProtocolRow> Rows { get; set; } = new List<ProtocolRow>();
    public ICollection<ProtocolSignature> Signatures { get; set; } = new List<ProtocolSignature>();
}

/// <summary>
/// Строка сравнительной таблицы в протоколе — копия предложения на момент
/// формирования, а не ссылка на живое КП.
/// </summary>
public class ProtocolRow
{
    public int Id { get; set; }

    public int ProtocolId { get; set; }
    public ProcurementProtocol? Protocol { get; set; }

    /// <summary>Порядковый номер варианта в таблице.</summary>
    public int Order { get; set; }

    public required string SupplierTitle { get; set; }
    public string? SupplierInn { get; set; }

    public decimal Price { get; set; }
    public string? Specification { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? PaymentTerms { get; set; }

    /// <summary>Заключение инициатора: соответствует / отклонено с причиной.</summary>
    public required string InitiatorConclusion { get; set; }

    public bool IsWinner { get; set; }
}

/// <summary>Подпись стороны под протоколом.</summary>
public class ProtocolSignature
{
    public int Id { get; set; }

    public int ProtocolId { get; set; }
    public ProcurementProtocol? Protocol { get; set; }

    public ProtocolSignerRole Role { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public SignatureLevel Level { get; set; }

    public DateTime At { get; set; }

    /// <summary>Содержание, под которым стоит подпись.</summary>
    public required string ContentHash { get; set; }

    public bool Revoked { get; set; }
    public string? RevokedReason { get; set; }
}
