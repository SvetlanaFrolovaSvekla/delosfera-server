using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Должности сотрудников банка.

"Гл. специалист", "Юрисконсульт", "Методолог", "Специалист по закупкам",
"Зам. Председателя Правления", "Делопроизводитель", "Начальник управления",
"Начальник департамента", "HR-директор", "Начальник отдела", "Главный бухгалтер",
"Директор казначейства", "Администратор"
*/

public class Position : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}