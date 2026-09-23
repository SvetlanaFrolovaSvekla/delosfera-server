using delosfera_server.Modules.Files.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Один Лист согласования редакции. У редакции их может быть несколько: первый
/// формируется, когда редакция согласуется впервые, а каждая последующая "актуализация без
/// изменений" с согласованием (см. VndDocument.ActualizationPlannedNoChanges и
/// VndApprovalProcess.IsNoChangesActualization) гоняет ту же самую редакцию через новый
/// процесс согласования - возможно, по другому маршруту, с другими согласующими - и даёт ей ещё
/// один лист. Раньше новый лист просто перезаписывал VndRedaction.ApprovalSheetFileId, и
/// предыдущий терялся из интерфейса.
///
/// VndRedaction.ApprovalSheetFileId при этом остаётся и всегда указывает на САМЫЙ ПОСЛЕДНИЙ
/// лист (обратная совместимость со всеми местами, где он уже используется).</summary>
public class VndRedactionApprovalSheet
{
    public int Id { get; set; }

    public int VndRedactionId { get; set; }
    public VndRedaction? VndRedaction { get; set; }

    /// <summary>Процесс согласования, по итогам которого сформирован лист. Null - лист приложен
    /// главным редактором вручную (VndService.EditRedactionDirectlyCoreAsync) либо перенесён
    /// миграцией из старого VndRedaction.ApprovalSheetFileId, для которого процесс не нашёлся.</summary>
    public int? ApprovalProcessId { get; set; }
    public VndApprovalProcess? ApprovalProcess { get; set; }

    public int FileAttachmentId { get; set; }
    public FileAttachment? FileAttachment { get; set; }

    /// <summary>Лист сформирован по итогам согласования в рамках актуализации без изменений (а
    /// не первичного согласования редакции) - в интерфейсе подписывается соответственно.</summary>
    public bool IsNoChangesActualization { get; set; }

    /// <summary>Дата окончательного согласования, которую фиксирует этот лист (для ручных листов -
    /// момент загрузки).</summary>
    public DateTime ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
