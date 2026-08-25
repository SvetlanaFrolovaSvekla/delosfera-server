namespace delosfera_server.Modules.Dictionaries.Models;

/// <summary>
/// Вид подразделения. Названия те же, что в портале банка: он ведёт структуру,
/// и заводить своё деление значило бы переводить одно в другое при каждом обмене.
/// </summary>
public enum OrgUnitKind
{
    /// <summary>Заведено руками или пришло до того, как вид стали переносить.</summary>
    Unknown = 0,

    /// <summary>Коллегиальный орган: правление, комитет.</summary>
    Board = 1,

    /// <summary>Управление, департамент — узел, которому подчинены отделы.</summary>
    Division = 2,

    /// <summary>Отдел, сектор — нижний уровень структуры.</summary>
    Department = 3,
}
