namespace delosfera_server.Modules.Vnd.DTO.Request;

public class ResubmitAfterRevisionRequest
{
    /// <summary>Обновлённые файлы редакции - если нужно заменить документ по итогам замечаний.
    /// Можно отправить на повторное согласование и без замены файлов.</summary>
    public IFormFile? DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }
    
    /// <summary>Комментарий инициатора о внесённых исправлениях</summary>
    public string? Comment { get; set; }

    /// <summary>Согласен ли инициатор со всеми замечаниями.
    /// false → нужна заполненная матрица разногласий, повторное согласование
    /// пропускается, процесс сразу переходит на финальную выдержку.</summary>
    public required bool AgreesWithAllRemarks { get; set; }
}