using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Сотрудник, которого касается кадровая записка.
///
/// Отдельной записью, а не строкой в карточке, потому что записка часто про
/// нескольких: в командировку едут группой, оклад повышают отделу, в выходной
/// выходит смена. Одно поле ФИО заставляло заводить отдельную записку на каждого
/// либо перечислять людей в тексте — и тогда по ним нельзя ни искать, ни отчитаться.
///
/// Сотрудник может быть как своим — тогда есть ссылка на учётную запись и ФИО
/// подтягивается из домена, — так и внешним: в записке о приёме на работу человека
/// в системе ещё нет, а записка уже нужна.
/// </summary>
public class SzEmployee
{
    public int Id { get; set; }

    public int SzDocumentId { get; set; }
    public SzDocument? SzDocument { get; set; }

    /// <summary>Учётная запись сотрудника. Пусто у кандидата, которого ещё не приняли.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// ФИО. У своих заполняется из учётной записи и хранится копией: человек может
    /// сменить фамилию, а записка должна остаться такой, какой её подписали.
    /// </summary>
    public required string FullName { get; set; }

    /// <summary>Подразделение сотрудника на момент записки.</summary>
    public int? OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    /// <summary>Должность строкой: у кандидата её ещё нет в справочнике.</summary>
    public string? Position { get; set; }

    /// <summary>
    /// Значения полей, своих для этого человека: у каждого свой оклад, свои даты
    /// командировки. Общие для всей записки поля лежат в самой записке.
    /// </summary>
    public string? ValuesJson { get; set; }

    public int SortOrder { get; set; }
}
