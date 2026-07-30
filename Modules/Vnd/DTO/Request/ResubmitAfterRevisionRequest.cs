namespace delosfera_server.Modules.Vnd.DTO.Request;

public class ResubmitAfterRevisionRequest
{
    /// <summary>Обновлённые файлы редакции - если нужно заменить документ по итогам замечаний.
    /// Можно отправить на повторное согласование и без замены
    /// файлов (далее добавлю матрицу разногласий для этого случая)</summary>
    public IFormFile? DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }
}