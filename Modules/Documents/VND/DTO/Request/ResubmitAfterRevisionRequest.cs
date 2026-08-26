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

    /// <summary>Согласен ли инициатор со всеми замечаниями.
    /// false → нужна заполненная матрица разногласий, повторное согласование
    /// пропускается, процесс сразу переходит на финальную выдержку.</summary>
    public required bool AgreesWithAllRemarks { get; set; }
}