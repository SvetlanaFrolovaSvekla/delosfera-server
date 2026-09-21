using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Substitutions.Models;

/// <summary>
/// Норматив срока согласования заявок на замещение (ЗМ-SLA). Одна строка на систему;
/// ведёт администратор без участия разработчика. Задаёт, за сколько рабочих дней должен
/// пройти один этап маршрута (директор филиала / Опер. управление / УЧР) — иначе этап
/// считается просроченным и текущему согласующему уходит напоминание.
/// </summary>
public class SubstitutionSlaSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Норматив на один этап согласования, рабочих дней. По умолчанию 3.</summary>
    public int ApprovalStepSlaDays { get; set; } = 3;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
