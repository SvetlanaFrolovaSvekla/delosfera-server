using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Набор дополнительных полей карточки, зависящий от вида СЗ. Виды пополняет администратор,
/// но каждый вид опирается на одну из известных форм — иначе пришлось бы выпускать релиз
/// ради нового вида.
/// </summary>
public enum SzFormKey
{
    /// <summary>Прочие: только общие поля.</summary>
    Other,

    /// <summary>Кадровые: вид кадровой СЗ, сотрудник, СП, СП перевода.</summary>
    Hr,

    /// <summary>На закупку: наличие бюджета и сумма.</summary>
    Procurement,

    /// <summary>На обучение: сотрудник, СП, командировочные расходы.</summary>
    Training
}

/// <summary>Вид служебной записки (справочник, пополняется администратором).</summary>
public class SzKind : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Какая форма дополнительных полей показывается для этого вида.</summary>
    public SzFormKey FormKey { get; set; } = SzFormKey.Other;

    /// <summary>Вид по умолчанию требует бумажного носителя (SZ-PAP-01).</summary>
    public bool IsPaperByDefault { get; set; }

    /// <summary>Норматив исполнения в днях; по инструкции по делопроизводству — 14.</summary>
    public int ExecutionDays { get; set; } = 14;

    /// <summary>
    /// Шаблон маршрута согласования по умолчанию (SZ-01). Если не задан, делопроизводитель
    /// указывает маршрут при регистрации.
    /// </summary>
    public int? RouteTemplateId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Вид кадровой служебной записки (изменение оклада, командировка, приём и т. д.) —
/// отдельный справочник, потому что перечень задан ТЗ и пополняется независимо от видов СЗ.
/// </summary>
public class SzHrKind : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
