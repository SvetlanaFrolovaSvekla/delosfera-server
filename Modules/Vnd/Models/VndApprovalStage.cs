using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Vnd.Models;

public class VndApprovalStage : IAuditableEntity
{
    public int Id { get; set; }

    public int ApprovalProcessId { get; set; }
    public VndApprovalProcess? ApprovalProcess { get; set; }
    
    /// <summary>Порядковый номер для отображения в маршрутном листе (1,2,3...).
    /// На права принятия решения НЕ влияет - согласование параллельное.</summary>
    public int Order { get; set; }

    public ApprovalStageKind Kind { get; set; }

    public int OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    public int ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }

    // --- Первичный этап
    public ApprovalStageDecision PrimaryDecision { get; set; } = ApprovalStageDecision.Pending;
    public string? PrimaryComment { get; set; }
    public DateTime? PrimaryDecidedAt { get; set; }

    /// <summary>Проставляется, если по первичному решению были замечания/отклонение, то
    /// определяет, кто участвует в повторном согласовании</summary>
    public bool ParticipatesInRepeat { get; set; }

    // --- Повторный этап (заполняется, только если ParticipatesInRepeat == true)
    public ApprovalStageDecision? RepeatDecision { get; set; }
    public string? RepeatComment { get; set; }
    public DateTime? RepeatDecidedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}