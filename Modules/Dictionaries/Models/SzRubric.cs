using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Рубрикатор служебных записок — тематическая классификация СЗ, отдельная от
 Рубрикатора ВНД (Rubric.cs): те же по смыслу данные (иерархический
 справочник, рубрика может иметь родителя), но своя таблица и свой набор
 значений — они ведутся независимо друг от друга.
*/

public class SzRubric : IAuditableEntity, ITranslatableEntity, IHierarchicalEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public int? ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public SzRubric? Parent { get; set; }
    public ICollection<SzRubric> Children { get; set; } = new List<SzRubric>();
}
