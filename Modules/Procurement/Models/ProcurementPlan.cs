using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Procurement.Models;

/// <summary>Стадия годового Плана закупок (PRC-22).</summary>
public enum PlanStatus
{
    /// <summary>Формируется подразделениями, позиции можно менять.</summary>
    Draft = 1,

    /// <summary>Утверждён Правлением — позиции меняются только через корректировку.</summary>
    Approved = 2,

    /// <summary>Год закрыт, сформирован отчёт об исполнении.</summary>
    Closed = 3,
}

/// <summary>
/// Годовой План закупок (PRC-22, приложение №5). Хранится по годам: заявка ссылается
/// на позицию плана, а закупка без позиции считается внеплановой и идёт по отдельной
/// ветке согласования (PRC-03).
/// </summary>
public class ProcurementPlan : IAuditableEntity
{
    public int Id { get; set; }

    public int Year { get; set; }

    public PlanStatus Status { get; set; } = PlanStatus.Draft;

    /// <summary>Протокол Правления, которым план утверждён.</summary>
    public string? ApprovalProtocol { get; set; }
    public DateOnly? ApprovedOn { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ProcurementPlanItem> Items { get; set; } = new List<ProcurementPlanItem>();
}

/// <summary>Позиция Плана закупок: что, на какую сумму и когда закупается.</summary>
public class ProcurementPlanItem
{
    public int Id { get; set; }

    public int PlanId { get; set; }
    public ProcurementPlan? Plan { get; set; }

    /// <summary>Номер позиции в плане: по нему заявка ссылается на план.</summary>
    public required string Code { get; set; }

    public required string Subject { get; set; }

    /// <summary>Плановая сумма закупки.</summary>
    public decimal PlannedAmount { get; set; }

    /// <summary>Квартал, на который запланирована закупка (1–4); null — без привязки.</summary>
    public int? Quarter { get; set; }

    public int? OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    public ProcurementSubjectKind SubjectKind { get; set; } = ProcurementSubjectKind.Goods;

    public string? Note { get; set; }
}
