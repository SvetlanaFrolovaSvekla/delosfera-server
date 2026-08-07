using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/*
 Обязательные участники процесса согласования (справочник).
 Для каждого фиксированного этапа маршрута (Юр. Управление, Риск-менеджмент,
 Комплаенс-контроль, Методология) хранится пользователь по умолчанию,
 который подставляется в конструкторе маршрута согласования.
*/

public class CoordinationDefaultApprover : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Фиксированный этап: Legal, RiskManagement, Compliance или Methodology</summary>
    public ApprovalStageKind Kind { get; set; }

    /// <summary>Согласующий по умолчанию для этого этапа. Может быть не задан (null) —
    /// тогда конструктор маршрута просто не подставит значение автоматически</summary>
    public int? ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}