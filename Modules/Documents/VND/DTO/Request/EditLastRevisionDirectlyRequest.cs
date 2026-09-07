namespace delosfera_server.Modules.Documents.VND.DTO.Request;
using System.ComponentModel.DataAnnotations;

/// <summary>Прямое редактирование редакции ВНД главным редактором (см. VndService.
/// EditRedactionDirectlyAsync/EditLastRevisionDirectlyAsync) - подмена файлов, специальных
/// вложений (ТИД/Лист согласования/Матрица разногласий) и/или описания без запуска согласования,
/// без создания новой редакции и без изменения даты актуализации. Изначально работало только для
/// последней редакции (см. EditLastRevisionDirectlyAsync, оставлен для обратной совместимости) -
/// теперь то же самое доступно для ЛЮБОЙ редакции документа через EditRedactionDirectlyAsync.</summary>
public class EditLastRevisionDirectlyRequest
{
    public IFormFile? DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }

    /// <summary>Убрать документ на кыргызском языке без замены. Игнорируется, если одновременно
    /// передан DocKg — замена имеет приоритет. Русский документ обязателен и убрать его нельзя.</summary>
    public bool RemoveDocKg { get; set; }

    /// <summary>Убрать документ на английском языке без замены. Игнорируется, если одновременно
    /// передан DocEn — замена имеет приоритет.</summary>
    public bool RemoveDocEn { get; set; }

    // --- Специальные вложения (ТИД/Лист согласования/Матрица разногласий) - в обычном режиме
    // формируются автоматически (см. VndService.UploadTidForLastRedactionAsync,
    // VndApprovalService.GenerateApprovalSheetAsync) или загружаются в рамках согласования
    // (ResubmitAfterRevisionAsync), но для мигрированных из isrib редакций их часто нет вовсе, а
    // главному редактору нужно их приложить или заменить вручную - см. RedactionAttachmentsModal /
    // VndEditLastRevisionModal.

    /// <summary>Загрузить или заменить ТИД. Можно приложить и туда, где его никогда не было
    /// (например, у мигрированной из isrib редакции) — как обычный аплоад.</summary>
    public IFormFile? Tid { get; set; }

    /// <summary>Убрать ТИД без замены. Игнорируется, если одновременно передан Tid.</summary>
    public bool RemoveTid { get; set; }

    /// <summary>Загрузить или заменить Лист согласования вручную (в норме формируется
    /// автоматически по завершении согласования — см. VndApprovalService.
    /// GenerateApprovalSheetAsync).</summary>
    public IFormFile? ApprovalSheet { get; set; }

    /// <summary>Убрать Лист согласования без замены. Игнорируется, если одновременно передан
    /// ApprovalSheet.</summary>
    public bool RemoveApprovalSheet { get; set; }

    /// <summary>Загрузить или заменить Матрицу разногласий вручную.</summary>
    public IFormFile? DisagreementMatrix { get; set; }

    /// <summary>Убрать Матрицу разногласий без замены. Игнорируется, если одновременно передан
    /// DisagreementMatrix.</summary>
    public bool RemoveDisagreementMatrix { get; set; }

    [StringLength(500, ErrorMessage = "Описание редакции не может превышать 500 символов!")]
    public string? Description { get; set; }

    /// <summary>Новые вложения, добавляемые к редакции (см. тот же паттерн в
    /// ResubmitAfterRevisionRequest).</summary>
    public List<IFormFile>? NewAttachments { get; set; }

    /// <summary>Id файлов существующих вложений редакции, которые нужно удалить.</summary>
    public List<int>? RemovedAttachmentFileIds { get; set; }
}