using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Уровни секретности документов. Обычно фиксированные, но добавлены возможности CRUD.
(Это один из классификаторов)
"Открытый доступ", "Конфиденциально", "Секретно", "Совершенно секретно"
*/

public class SecurityLevel : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}