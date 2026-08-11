using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>
/// Справочник способов закупки (PRC-02): прямое заключение, простая закупка,
/// конкурс с ограниченным и неограниченным участием.
/// </summary>
public class ProcurementMethod : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }

    public ProcurementMethodCode Code { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Короткая подпись для матрицы и колонок протокола: «Простая», «Конкурс».</summary>
    public required string ShortTitleRu { get; set; }

    /// <summary>
    /// Минимальное число коммерческих предложений (PRC-09). Для простой закупки — 3,
    /// для конкурса заявки собираются процедурой, поэтому 0.
    /// </summary>
    public int MinProposals { get; set; }

    /// <summary>Требуется ли обоснование выбора способа — обязательно для прямого заключения.</summary>
    public bool RequiresJustification { get; set; }

    /// <summary>Публикуется ли объявление (PRC-13): только конкурс с неограниченным участием.</summary>
    public bool RequiresPublication { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
