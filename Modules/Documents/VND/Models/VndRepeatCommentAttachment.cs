using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Файл, приложенный инициатором к комментарию о внесённых исправлениях
/// (<see cref="VndApprovalProcess.RepeatInitiatorComment"/>) при повторной отправке редакции
/// после замечаний — см. VndApprovalService.ResubmitAfterRevisionAsync.
///
/// Комментарий перезаписывается на каждый круг доработки, поэтому вложения к нему тоже
/// полностью заменяются при каждой отправке (старые удаляются вместе с файлами в хранилище) —
/// это единственный случай, когда вложения удаляются.
///
/// Вложения к последней отправке хранятся бессрочно, наравне с текстом комментария: когда
/// редакция ВНД становится согласованной, они НЕ удаляются и остаются частью истории
/// согласования.</summary>
public class VndRepeatCommentAttachment
{
    public int Id { get; set; }

    public int VndApprovalProcessId { get; set; }
    public VndApprovalProcess? VndApprovalProcess { get; set; }

    public int FileAttachmentId { get; set; }
    public FileAttachment? FileAttachment { get; set; }

    public DateTime CreatedAt { get; set; }
}
