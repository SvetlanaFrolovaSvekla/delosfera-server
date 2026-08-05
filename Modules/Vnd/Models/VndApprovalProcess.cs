using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Vnd.Models;

public class VndApprovalProcess : IAuditableEntity
{
    public int Id { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    /// <summary>Редакция, которая проходит согласование</summary>
    public int RedactionId { get; set; }
    public VndRedaction? Redaction { get; set; }

    public int InitiatorUserId { get; set; }

    // --- Текущий статус ВНД (Primary/RevisionNeeded/Repeated/FinalHold/Approved/Cancelled/Rejected)
    public ApprovalProcessStatus Status { get; set; } = ApprovalProcessStatus.Primary;

    // --- Нормативы в минутах. Отсчёт каждого — от момента старта именно этого этапа
    public int PrimaryDeadlineMinutes  { get; set; } // Первичная выдержка 
    public int RepeatDeadlineMinutes  { get; set; } // Согласование после устранения замечаний
    public int FinalHoldDeadlineMinutes  { get; set; } // Финальная выдержка

    // Моменты отсчёта выдержек
    public DateTime PrimaryStartedAt { get; set; }
    public DateTime? RepeatStartedAt { get; set; }
    public DateTime? FinalHoldStartedAt { get; set; }
    
    public string? RepeatInitiatorComment { get; set; }

    public DateTime? CompletedAt { get; set; } // Когда процесс завершился, ВНД стал действующим 

    public ICollection<VndApprovalStage> Stages { get; set; } = new List<VndApprovalStage>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Конкретные дедлайны для согласующих  
    public DateTime PrimaryDeadlineAt => PrimaryStartedAt.AddMinutes(PrimaryDeadlineMinutes);
    public DateTime? RepeatDeadlineAt => RepeatStartedAt?.AddMinutes(RepeatDeadlineMinutes);
    public DateTime? FinalHoldDeadlineAt => FinalHoldStartedAt?.AddMinutes(FinalHoldDeadlineMinutes);
    
    // Матрица разногласий
    public ICollection<VndDisagreementMatrixRow> DisagreementMatrixRows { get; set; } = new List<VndDisagreementMatrixRow>();
}