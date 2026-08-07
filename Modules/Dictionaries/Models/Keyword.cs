using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Ключевые слова для тегирования и поиска документов. Иерархический справочник -
 ключевое слово может иметь родителя (ParentId).
*/

public class Keyword : IAuditableEntity, ITranslatableEntity, IHierarchicalEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public int? ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Keyword? Parent { get; set; }
    public ICollection<Keyword> Children { get; set; } = new List<Keyword>();
}