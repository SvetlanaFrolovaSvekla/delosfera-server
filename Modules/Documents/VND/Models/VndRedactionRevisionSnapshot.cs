using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Снимок файлов редакции ВНД, сделанный непосредственно перед тем, как инициатор
/// перезаписывает их при отправке исправленной версии на повторное согласование (см.
/// VndApprovalService.ResubmitAfterRevisionAsync). Нужен, чтобы проверяющие могли сравнить
/// версию документа, к которой относились их замечания, с версией, присланной в ответ —
/// например, при работе над редакцией "10296-Р1" в процессе согласования получаются
/// промежуточные версии "10296-Р1.1", "10296-Р1.2" и т.д. (см. SnapshotNumber).
///
/// В отличие от <see cref="VndApprovalPhaseRound"/> (который сохраняет только РЕШЕНИЯ и
/// комментарии согласующих за круг), этот снимок сохраняет сами ФАЙЛЫ редакции на момент
/// круга — его нужно скачать и посмотреть, а не просто прочитать в истории.
///
/// Phase/RoundNumber снимка — это фаза и круг, чьи замечания относятся именно к сохранённой
/// здесь версии файлов (то есть круг, который завершается этой отправкой) — при создании
/// вычисляются тем же способом, что и у соответствующего VndApprovalPhaseRound, создаваемого
/// (если замечания устранены полностью) тем же вызовом ResubmitAfterRevisionAsync — см.
/// VndApprovalService.DetermineActiveRevisionPhaseAsync.</summary>
public class VndRedactionRevisionSnapshot
{
    public int Id { get; set; }

    public int VndRedactionId { get; set; }
    public VndRedaction? VndRedaction { get; set; }

    public int ApprovalProcessId { get; set; }
    public VndApprovalProcess? ApprovalProcess { get; set; }

    /// <summary>Порядковый номер снимка в рамках этой редакции, начиная с 1 — для отображения
    /// как "10296-Р1.1", "10296-Р1.2" и т.д.</summary>
    public int SnapshotNumber { get; set; }

    /// <summary>Repeat/FinalHold — фаза круга, чьи замечания относятся к этой версии файлов;
    /// Primary — самая первая версия, поданная до начала согласования (замечания первичного
    /// согласования относятся именно к ней).</summary>
    public ApprovalStagePhase Phase { get; set; }

    /// <summary>Номер круга внутри фазы (см. VndApprovalPhaseRound.RoundNumber) — null для
    /// Phase == Primary, у неё всегда ровно один круг.</summary>
    public int? RoundNumber { get; set; }

    // --- Снимки файловых полей VndRedaction на момент ДО перезаписи в ResubmitAfterRevisionAsync.
    public int DocFileRuId { get; set; }
    public FileAttachment? DocFileRu { get; set; }

    public int? DocFileKgId { get; set; }
    public FileAttachment? DocFileKg { get; set; }

    public int? DocFileEnId { get; set; }
    public FileAttachment? DocFileEn { get; set; }

    public int? TidFileId { get; set; }
    public FileAttachment? TidFile { get; set; }

    public int? DisagreementMatrixFileId { get; set; }
    public FileAttachment? DisagreementMatrixFile { get; set; }

    public DateTime CreatedAt { get; set; }
}
