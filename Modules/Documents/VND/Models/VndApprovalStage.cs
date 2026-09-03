using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

public class VndApprovalStage : IAuditableEntity
{
    public int Id { get; set; }

    public int ApprovalProcessId { get; set; }
    public VndApprovalProcess? ApprovalProcess { get; set; }
    
    /// <summary>Порядковый номер для отображения в маршрутном листе (1,2,3...).
    /// На права принятия решения НЕ влияет - согласование параллельное.</summary>
    public int Order { get; set; }

    public ApprovalStageKind Kind { get; set; }

    /// <summary>Снимок названия этапа на момент построения маршрута (из
    /// CoordinationDefaultApprover.Title для фиксированных этапов, либо "Доп. этап" для
    /// произвольных). Не меняется, даже если запись справочника потом переименуют/удалят -
    /// история согласования должна показывать этап таким, каким он был при запуске.</summary>
    public string? Title { get; set; }

    /// <summary>Запись справочника обязательных этапов (dictionaries/coordination-users), из
    /// которой был построен этот этап - null для произвольных (Custom) этапов, добавленных
    /// инициатором вручную. При удалении записи справочника обнуляется (SetNull) - сам этап и
    /// его данные (OrgUnitId/ApproverUserId/Title) при этом не теряются, это лишь ссылка для
    /// прослеживаемости.</summary>
    public int? CoordinationStageId { get; set; }
    public CoordinationDefaultApprover? CoordinationStage { get; set; }

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
    
    // --- Финальная выдержка (тоже может быть с замечанием)
    public ApprovalStageDecision? FinalHoldDecision { get; set; }
    public string? FinalHoldComment { get; set; }
    public DateTime? FinalHoldDecidedAt { get; set; }

    /// <summary>Файлы, приложенные согласующим к резолюции (по всем фазам). Очищаются, когда
    /// редакция становится согласованной — см. <see cref="VndApprovalStageAttachment"/>.</summary>
    public ICollection<VndApprovalStageAttachment> Attachments { get; set; } = new List<VndApprovalStageAttachment>();

    /// <summary>Цитаты из текста редакции, на которые согласующий сослался в резолюции (по всем
    /// фазам) — см. <see cref="VndApprovalStageQuote"/>.</summary>
    public ICollection<VndApprovalStageQuote> Quotes { get; set; } = new List<VndApprovalStageQuote>();
}