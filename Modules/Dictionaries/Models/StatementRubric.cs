using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Рубрикатор заявлений — тематическая классификация заявлений сотрудников.
 Устроен так же, как Рубрикатор СЗ и Рубрикатор ВНД (иерархический справочник,
 рубрика может иметь родителя), но таблица и набор значений свои — справочники
 ведутся независимо друг от друга.
*/

public class StatementRubric : IAuditableEntity, ITranslatableEntity, IHierarchicalEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public int? ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public StatementRubric? Parent { get; set; }
    public ICollection<StatementRubric> Children { get; set; } = new List<StatementRubric>();
}
