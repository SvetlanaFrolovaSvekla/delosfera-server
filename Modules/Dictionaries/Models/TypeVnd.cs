using delosfera_server.Common.Models;
namespace delosfera_server.Modules.Dictionaries.Models;

/*
 Виды (типы) ВНД. Обычно они фиксированные, но добавлены возможности CRUD.
 
"Базовые условия кредитного продукта", "Договор", "Должностная инструкция", 
"Инструкция", "Кодекс", "Концепция", "Лимиты", "Матрица", "Методика", "План",
"Политика", "Положение", "Порядок", "Правила", "Программа", "Процедура", "Регламент", 
"Руководство", "Система", "Устав"
*/

public class TypeVnd : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}