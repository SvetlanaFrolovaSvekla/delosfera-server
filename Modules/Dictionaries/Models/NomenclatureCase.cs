using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/// <summary>
/// Дело номенклатуры (GEN-09): папка, в которую подшиваются исполненные документы.
/// Номенклатура годовая — одно и то же дело заводится заново на каждый год,
/// поэтому индекс уникален в паре с годом.
/// </summary>
public class NomenclatureCase : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }

    /// <summary>Индекс дела по номенклатуре, например «05-12».</summary>
    public required string Index { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Год номенклатуры, к которому относится дело.</summary>
    public int Year { get; set; }

    /// <summary>Подразделение, которое ведёт дело.</summary>
    public int? OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    /// <summary>Срок хранения дела — подставляется документу при подшивке.</summary>
    public int? StorageTermId { get; set; }
    public StorageTerm? StorageTerm { get; set; }

    /// <summary>
    /// Дело закрыто: срок хранения документов отсчитывается от закрытия дела,
    /// а не от даты самого документа.
    /// </summary>
    public DateOnly? ClosedOn { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
