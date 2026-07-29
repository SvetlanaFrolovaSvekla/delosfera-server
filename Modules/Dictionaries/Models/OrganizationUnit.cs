using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Структурные подразделения банка (СП). 
 Иерархический справочник - подразделение может иметь родителя.
*/

public class OrganizationUnit : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public int? ParentId { get; set; }

    /// <summary>Начальник подразделения — используется как ответственный исполнитель по умолчанию</summary>
    public int? HeadUserId { get; set; }
    public User? HeadUser { get; set; }

    /// <summary>Куратор подразделения — используется как куратор-разработчик ВНД по умолчанию</summary>
    public int? CuratorUserId { get; set; }
    public User? CuratorUser { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public OrganizationUnit? Parent { get; set; }
    public ICollection<OrganizationUnit> Children { get; set; } = new List<OrganizationUnit>();
}