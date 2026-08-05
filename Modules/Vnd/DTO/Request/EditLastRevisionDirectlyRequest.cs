namespace delosfera_server.Modules.Vnd.DTO.Request;

/// <summary>Прямое редактирование последней редакции ВНД (для EditLastRevisionDirectly) -
/// подмена файлов и/или описания без запуска согласования, без создания новой редакции
/// и без изменения даты актуализации.</summary>
public class EditLastRevisionDirectlyRequest
{
    public IFormFile? DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }
    public string? Description { get; set; }
}