using System.ComponentModel.DataAnnotations;
using delosfera_server.Common.Validation;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class ResubmitAfterRevisionRequest
{
    /// <summary>Обновлённые файлы редакции - если нужно заменить документ по итогам замечаний.
    /// Можно отправить на повторное согласование и без замены файлов.</summary>
    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? DocRu { get; set; }
    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? DocKg { get; set; }
    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? DocEn { get; set; }

    /// <summary>Обновлённый файл ТИД — обязателен при повторной отправке, если у редакции уже был
    /// обязателен ТИД при первичной подаче (см. VndRedaction.Number > 1). Прикладывается заново
    /// на каждый круг доработки вместе с обновлённой редакцией.</summary>
    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? Tid { get; set; }
    
    /// <summary>Комментарий инициатора о внесённых исправлениях</summary>
    [StringLength(5000)]
    public string? Comment { get; set; }

    /// <summary>Согласен ли инициатор со всеми замечаниями.
    /// Если false, то нужна заполненная матрица разногласий, повторное согласование
    /// пропускается, процесс сразу переходит на финальную выдержку.</summary>
    public required bool AgreesWithAllRemarks { get; set; }
}