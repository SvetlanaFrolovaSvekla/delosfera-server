namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class ResubmitAfterRevisionRequest
{
    /// <summary>Обновлённые файлы редакции - если нужно заменить документ по итогам замечаний.
    /// Можно отправить на повторное согласование и без замены файлов.</summary>
    public IFormFile? DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }

    /// <summary>Обновлённый файл ТИД — обязателен при повторной отправке, если у редакции уже был
    /// обязателен ТИД при первичной подаче (см. VndRedaction.Number > 1). Прикладывается заново
    /// на каждый круг доработки вместе с обновлённой редакцией.</summary>
    public IFormFile? Tid { get; set; }

    /// <summary>Убрать документ на кыргызском языке без замены (редакция останется без него).
    /// Игнорируется, если одновременно передан DocKg — замена имеет приоритет.</summary>
    public bool RemoveDocKg { get; set; }

    /// <summary>Убрать документ на английском языке без замены (редакция останется без него).
    /// Игнорируется, если одновременно передан DocEn — замена имеет приоритет.</summary>
    public bool RemoveDocEn { get; set; }

    /// <summary>Новые вложения, добавляемые к редакции вместе с исправленной редакцией</summary>
    public List<IFormFile>? NewAttachments { get; set; }

    /// <summary>Id файлов существующих вложений редакции, которые нужно удалить</summary>
    public List<int>? RemovedAttachmentFileIds { get; set; }

    /// <summary>Комментарий инициатора о внесённых исправлениях</summary>
    public string? Comment { get; set; }

    /// <summary>Файлы, приложенные инициатором к комментарию о внесённых исправлениях
    /// (необязательно). Полностью заменяют предыдущий набор — комментарий перезаписывается
    /// на каждый круг доработки, поэтому и его вложения актуальны только для последней
    /// отправки.</summary>
    public List<IFormFile>? CommentAttachments { get; set; }

    /// <summary>Согласен ли инициатор со всеми замечаниями.
    /// FullyAgree → обычное повторное согласование.
    /// PartiallyAgree/FullyDisagree → нужна заполненная матрица разногласий (см.
    /// DisagreementMatrix), повторное согласование пропускается, процесс сразу переходит
    /// на финальную выдержку (для PartiallyAgree — так же, как для FullyDisagree; разница
    /// только в том, что при PartiallyAgree инициатор ещё и обновляет саму редакцию).</summary>
    public required RemarksAgreement RemarksAgreement { get; set; }

    /// <summary>Матрица разногласий — .docx-файл, обязателен, если RemarksAgreement != FullyAgree.
    /// Либо сформирован на клиенте по строкам матрицы (см. AddDisagreementMatrixRowAsync),
    /// либо загружен инициатором готовым файлом — с точки зрения бэка это просто файл,
    /// который сохраняется как VndRedaction.DisagreementMatrixFileId.</summary>
    public IFormFile? DisagreementMatrix { get; set; }
}

/// <summary>Согласен ли инициатор со всеми замечаниями, поднятыми на согласовании
/// (см. ResubmitAfterRevisionRequest.RemarksAgreement).</summary>
public enum RemarksAgreement
{
    /// <summary>Согласен со всеми — обычное повторное согласование.</summary>
    FullyAgree = 0,

    /// <summary>Согласен частично — редакция обновляется, но по несогласованным замечаниям
    /// заполняется матрица разногласий. Маршрут — как при полном несогласии: сразу на
    /// финальную выдержку, повторное согласование пропускается.</summary>
    PartiallyAgree = 1,

    /// <summary>Не согласен ни с одним замечанием — заполняется матрица разногласий,
    /// сразу на финальную выдержку.</summary>
    FullyDisagree = 2
}