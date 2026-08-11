using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.Models;

/// <summary>
/// Связь между документами (GEN-05): СЗ→Закупка, ТИД→ВНД, Договор→Закупка и т.п.
/// Аудируемая: без этого дата установления связи оставалась бы пустой,
/// а она показывается пользователю как дата запуска смежного процесса.
/// </summary>
public class DocumentLink : IAuditableEntity
{
    public int Id { get; set; }

    public int FromDocumentId { get; set; }
    public Document? FromDocument { get; set; }

    public int ToDocumentId { get; set; }
    public Document? ToDocument { get; set; }

    /// <summary>Тип связи (например "SzToProcurement", "TidToVnd", "ContractToProcurement").</summary>
    public required string LinkType { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
