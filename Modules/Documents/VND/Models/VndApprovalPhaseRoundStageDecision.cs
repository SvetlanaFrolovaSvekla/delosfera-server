namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Решение ОДНОГО этапа (согласующего) в рамках одного сохранённого круга
/// <see cref="VndApprovalPhaseRound"/> - снимок VndApprovalStage.RepeatDecision/RepeatComment/
/// RepeatDecidedAt (или FinalHold-аналогов) непосредственно перед тем, как круг был
/// перезаписан следующим. Создаётся только для этапов, у которых на момент снимка уже было
/// принято решение (Pending/ещё не решившие в круг не попадают - им нечего сохранять).</summary>
public class VndApprovalPhaseRoundStageDecision
{
    public int Id { get; set; }

    public int VndApprovalPhaseRoundId { get; set; }
    public VndApprovalPhaseRound? VndApprovalPhaseRound { get; set; }

    public int VndApprovalStageId { get; set; }
    public VndApprovalStage? VndApprovalStage { get; set; }

    public ApprovalStageDecision Decision { get; set; }
    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }
}
