using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Правило Матрицы полномочий по закупкам (PRC-05, приложение №1 к Положению).
/// Определяет по способу, диапазону суммы и признаку аффилированности: кто согласует
/// закупку, нужна ли комиссия и какой орган утверждает расход.
///
/// Пороги хранятся не в сомах, а парой «база + значение»: часть строк Положения задана
/// процентом балансовой стоимости активов или ЧСК, и при изменении баланса такие пороги
/// должны пересчитываться сами, без правки справочника.
/// </summary>
public class AuthorityMatrixRule : IAuditableEntity
{
    public int Id { get; set; }

    public int MethodId { get; set; }
    public ProcurementMethod? Method { get; set; }

    /// <summary>Правило для сделок с аффилированными лицами — у них своя шкала от ЧСК.</summary>
    public bool IsAffiliated { get; set; }

    /// <summary>
    /// Шкала нижней границы. У границ она своя у каждой: в Положении есть диапазоны вида
    /// «от 500 000 сом до 20% активов», где низ задан суммой, а верх — процентом.
    /// </summary>
    public ThresholdBase MinBase { get; set; }

    /// <summary>Шкала верхней границы.</summary>
    public ThresholdBase MaxBase { get; set; }

    /// <summary>
    /// Нижняя граница включительно: сумма в сомах либо процент (20 = 20%).
    /// null — граница снизу не задана.
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>Верхняя граница включительно; null — «и свыше».</summary>
    public decimal? MaxValue { get; set; }

    /// <summary>Состав согласования закупки: «Куратор», «Куратор + Правление».</summary>
    public required string ApprovalChainRu { get; set; }

    /// <summary>Требуется ли комиссия по закупке (PRC-14).</summary>
    public bool CommissionRequired { get; set; }

    /// <summary>Численность комиссии — по Положению нечётная, стандартно 5.</summary>
    public int? CommissionSize { get; set; }

    /// <summary>Сколько членов Правления обязано войти в комиссию.</summary>
    public int? CommissionMinBoardMembers { get; set; }

    /// <summary>Орган, утверждающий расход.</summary>
    public ApprovalAuthority ApprovalAuthority { get; set; }

    /// <summary>Как строка выглядит в приложении №1 — печатается в матрице и протоколе.</summary>
    public required string CommissionNoteRu { get; set; }

    /// <summary>Порядок вывода в таблице приложения №1.</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
