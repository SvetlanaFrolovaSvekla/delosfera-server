using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Органы утверждения ВНД. Иерархический справочник - орган может иметь родителя (ParentId).

"Комитет по управлению активами и пассивами", "Общее собрание акционеров", "Правление"
(с дочерними: "Заместитель Председателя Правления", "Председатель Правления", "Член Правления"),
"Совет директоров", "Тарифный комитет"
*/

public class ApprovalBody : IAuditableEntity, ITranslatableEntity, IHierarchicalEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public int? ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ApprovalBody? Parent { get; set; }
    public ICollection<ApprovalBody> Children { get; set; } = new List<ApprovalBody>();
}