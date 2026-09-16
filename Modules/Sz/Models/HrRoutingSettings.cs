using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Кто в маршруте кадровых СЗ выступает кадровиком УЧР — по области автора (КСЗ-04..06).
/// Разделение «Головной офис / филиальная сеть» из ТР: УЧР по ГО и УЧР по филиалам —
/// разные люди. Одна строка на систему; ведёт УЧР без участия разработчика (КСЗ-12).
/// </summary>
public class HrRoutingSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Кадровик УЧР для записок Головного офиса.</summary>
    public int? HeadOfficeHrUserId { get; set; }

    /// <summary>Кадровик УЧР для записок филиальной сети.</summary>
    public int? BranchHrUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
