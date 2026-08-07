namespace delosfera_server.Common.Models;

/// <summary>
/// Помечает сущность справочника как иерархическую (дерево родитель/потомок).
/// Проверка существования родителя, защита от циклических ссылок и от
/// превышения максимальной глубины вложенности.
/// </summary>
public interface IHierarchicalEntity
{
    int Id { get; }
    int? ParentId { get; set; }
}