namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Тип предмета закупки. Классификатор ветвит маршрут: хозтовары проверяет
/// Административный отдел, специфичный товар — технический координатор СП (PRC-08/11).
/// Полный классификатор — открытый вопрос В-5, поэтому здесь укрупнённые группы.
/// </summary>
public enum ProcurementSubjectKind
{
    /// <summary>Товары общего назначения.</summary>
    Goods = 1,

    /// <summary>Хозяйственные товары — дополнительная проверка Административным отделом.</summary>
    HouseholdGoods = 2,

    /// <summary>Специфичный товар — заключение технического координатора инициирующего СП.</summary>
    SpecificGoods = 3,

    /// <summary>Товар, требующий установки и ввода в эксплуатацию — договор готовит инициатор (PRC-18).</summary>
    GoodsWithInstallation = 4,

    /// <summary>Работы.</summary>
    Works = 5,

    /// <summary>Услуги.</summary>
    Services = 6,
}

/// <summary>
/// Статусы заявки на закупку. Строковые коды, как у остальных типов документов:
/// набор статусов свой у каждого контура, а карточка документа общая.
/// </summary>
public static class ProcurementStatus
{
    /// <summary>Черновик инициатора.</summary>
    public const string Draft = "Draft";

    /// <summary>Маршрут согласования запущен (PRC-08).</summary>
    public const string OnApproval = "OnApproval";

    /// <summary>Согласована; ждёт передачи в Сектор закупок.</summary>
    public const string Approved = "Approved";

    /// <summary>Передана в Сектор закупок АО — идёт закупочная процедура (PRC-09).</summary>
    public const string InProcurement = "InProcurement";

    /// <summary>Победитель определён, протокол оформлен.</summary>
    public const string Completed = "Completed";

    /// <summary>Возвращена инициатору на доработку.</summary>
    public const string OnRevision = "OnRevision";

    /// <summary>Отклонена согласующим.</summary>
    public const string Rejected = "Rejected";

    /// <summary>Отозвана инициатором.</summary>
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        Draft, OnApproval, Approved, InProcurement, Completed, OnRevision, Rejected, Cancelled,
    ];
}
