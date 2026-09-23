using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Файл, приложенный согласующим к своей резолюции (решению) на конкретном этапе
/// согласования — отдельно для первичного, повторного согласования и финальной выдержки
/// (см. <see cref="ApprovalStagePhase"/>).
///
/// Хранится бессрочно, наравне с текстом самой резолюции (Primary/Repeat/FinalHoldComment
/// на <see cref="VndApprovalStage"/>) — когда редакция ВНД становится согласованной
/// (<see cref="VndRedaction.ApprovalStatus"/> переходит в Approved), вложения НЕ удаляются
/// и остаются частью истории согласования.</summary>
public class VndApprovalStageAttachment
{
    public int Id { get; set; }

    public int VndApprovalStageId { get; set; }
    public VndApprovalStage? VndApprovalStage { get; set; }

    /// <summary>К решению какой фазы относится вложение</summary>
    public ApprovalStagePhase Phase { get; set; }

    public int FileAttachmentId { get; set; }
    public FileAttachment? FileAttachment { get; set; }

    /// <summary>Круг фазы Repeat/FinalHold, к решению которого относилось вложение, если этот круг
    /// уже завершён и перезаписан следующим (см. <see cref="VndApprovalPhaseRound"/> и
    /// VndApprovalService.ArchivePreviousRoundAttachments). Null - вложение относится к ТЕКУЩЕМУ
    /// решению этапа по фазе Phase (Primary всегда null - у первичного согласования один круг).
    ///
    /// Раньше вложения предыдущего круга физически удалялись вместе с файлами, из-за чего в
    /// истории согласования нельзя было открыть файлы, приложенные к замечаниям прошлых кругов.
    /// Теперь они сохраняются и "переезжают" в архив круга.</summary>
    public int? PhaseRoundId { get; set; }
    public VndApprovalPhaseRound? PhaseRound { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum ApprovalStagePhase
{
    Primary = 0,
    Repeat = 1,
    FinalHold = 2
}
