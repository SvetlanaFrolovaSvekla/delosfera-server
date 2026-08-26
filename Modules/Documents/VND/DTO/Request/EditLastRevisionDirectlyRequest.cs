namespace delosfera_server.Modules.Documents.VND.DTO.Request;
using System.ComponentModel.DataAnnotations;

/// <summary>Прямое редактирование последней редакции ВНД (для EditLastRevisionDirectly) -
/// подмена файлов и/или описания без запуска согласования, без создания новой редакции
/// и без изменения даты актуализации.</summary>
public class EditLastRevisionDirectlyRequest
{
    public IFormFile? DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }
    [StringLength(500, ErrorMessage = "Описание редакции не может превышать 500 символов!")]
    public string? Description { get; set; }
}