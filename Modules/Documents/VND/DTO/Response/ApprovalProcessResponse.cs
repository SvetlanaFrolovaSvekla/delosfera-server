namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class ApprovalProcessResponse
{
    public int Id { get; set; }
    public int VndId { get; set; }
    public int RedactionId { get; set; }
    public int InitiatorUserId { get; set; }
    public required string InitiatorName { get; set; }

    /// <summary>Должность инициатора (если назначена)</summary>
    public string? InitiatorPosition { get; set; }
    public required string Status { get; set; } // primary/revision_needed/repeated/final_hold/approved/cancelled

    public string? RepeatInitiatorComment { get; set; }
    
    public int PrimaryDeadlineMinutes { get; set; }
    public int RepeatDeadlineMinutes { get; set; }
    public int FinalHoldDeadlineMinutes { get; set; }

    public DateTime PrimaryStartedAt { get; set; }
    public DateTime PrimaryDeadlineAt { get; set; }
    public DateTime? RepeatStartedAt { get; set; }
    public DateTime? RepeatDeadlineAt { get; set; }
    public DateTime? FinalHoldStartedAt { get; set; }
    public DateTime? FinalHoldDeadlineAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<DisagreementMatrixRowResponse> DisagreementMatrixRows { get; set; } = [];
    
    public List<ApprovalStageResponse> Stages { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ApprovalStageResponse
{
    public int Id { get; set; }
    public int Order { get; set; }
    public required string Kind { get; set; } 

    public int OrgUnitId { get; set; }
    public required string OrgUnitName { get; set; }

    public int ApproverUserId { get; set; }
    public required string ApproverName { get; set; }

    public required string PrimaryDecision { get; set; }
    public string? PrimaryComment { get; set; }
    public DateTime? PrimaryDecidedAt { get; set; }
    public List<ApprovalStageAttachmentResponse> PrimaryAttachments { get; set; } = [];

    public bool ParticipatesInRepeat { get; set; }

    public string? RepeatDecision { get; set; }
    public string? RepeatComment { get; set; }
    public DateTime? RepeatDecidedAt { get; set; }
    public List<ApprovalStageAttachmentResponse> RepeatAttachments { get; set; } = [];

    public string? FinalHoldDecision { get; set; }
    public string? FinalHoldComment { get; set; }
    public DateTime? FinalHoldDecidedAt { get; set; }
    public List<ApprovalStageAttachmentResponse> FinalHoldAttachments { get; set; } = [];
}

/// <summary>Файл, приложенный согласующим к резолюции. Список пуст, если редакция уже
/// согласована — вложения к этому моменту физически удалены, остаётся только текст комментария.</summary>
public class ApprovalStageAttachmentResponse
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public required string FileName { get; set; }
    public long SizeBytes { get; set; }
}