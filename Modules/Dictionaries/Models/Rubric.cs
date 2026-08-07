using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Рубрикатор — тематическая классификация документов. Иерархический справочник -
 рубрика может иметь родителя (ParentId).
*/

public class Rubric : IAuditableEntity, ITranslatableEntity, IHierarchicalEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public int? ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Rubric? Parent { get; set; }
    public ICollection<Rubric> Children { get; set; } = new List<Rubric>();
}