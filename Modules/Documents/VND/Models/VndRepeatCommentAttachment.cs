using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Файл, приложенный инициатором к комментарию о внесённых исправлениях
/// (<see cref="VndApprovalProcess.RepeatInitiatorComment"/>) при повторной отправке редакции
/// после замечаний — см. VndApprovalService.ResubmitAfterRevisionAsync.
///
/// Комментарий перезаписывается на каждый круг доработки, поэтому вложения к нему тоже
/// полностью заменяются при каждой отправке (старые удаляются вместе с файлами в хранилище).
///
/// Живёт только пока идёт согласование: когда редакция ВНД становится согласованной, все
/// вложения этого процесса физически удаляются вместе с файлами в хранилище (см.
/// VndApprovalService.CleanupStageAttachmentsAsync) — текст комментария при этом не трогается
/// и остаётся в истории согласования.</summary>
public class VndRepeatCommentAttachment
{
    public int Id { get; set; }

    public int VndApprovalProcessId { get; set; }
    public VndApprovalProcess? VndApprovalProcess { get; set; }

    public int FileAttachmentId { get; set; }
    public FileAttachment? FileAttachment { get; set; }

    public DateTime CreatedAt { get; set; }
}
