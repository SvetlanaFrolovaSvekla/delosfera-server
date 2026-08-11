using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Поставщик (PRC-07/17). Ведётся как справочник: один и тот же поставщик приходит
/// в разные закупки, а признаки благонадёжности и чёрного списка должны действовать
/// на все его предложения сразу, а не заводиться заново в каждой заявке.
/// </summary>
public class Supplier : IAuditableEntity
{
    public int Id { get; set; }

    public required string Title { get; set; }

    /// <summary>ИНН — по нему идут проверки по чёрному списку и списку аффилированных.</summary>
    public string? Inn { get; set; }

    public string? Address { get; set; }

    /// <summary>ФИО руководителя — требуется в записи чёрного списка (приложение №4).</summary>
    public string? DirectorName { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Заключение Департамента безопасности о благонадёжности (PRC-07).
    /// Null — проверка не проводилась.
    /// </summary>
    public bool? IsReliable { get; set; }

    /// <summary>Дата заключения ДБ.</summary>
    public DateOnly? ReliabilityCheckedOn { get; set; }

    /// <summary>Справки об отсутствии налоговой задолженности и задолженности в Соцфонд.</summary>
    public bool HasTaxClearance { get; set; }
    public bool HasSocialFundClearance { get; set; }

    /// <summary>Аффилированное с Банком лицо — сделка идёт по отдельной шкале порогов.</summary>
    public bool IsAffiliated { get; set; }

    // --- чёрный список недобросовестных поставщиков (PRC-17, приложение №4) ---

    public bool IsBlacklisted { get; set; }

    /// <summary>Обоснование включения в чёрный список.</summary>
    public string? BlacklistReason { get; set; }

    /// <summary>До какой даты действует ограничение.</summary>
    public DateOnly? BlacklistedUntil { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
