namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class CreateVndRedactionRequest
{
    public required IFormFile DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }

    /// <summary>Таблица изменений и дополнений (ТИД). Обязателен, если у ВНД уже есть предыдущая
    /// редакция — то есть документ актуализируется, а не создаётся впервые. Для самой первой
    /// редакции нового ВНД не требуется.</summary>
    public IFormFile? Tid { get; set; }
    
    public string? Description { get; set; }
    
    public List<IFormFile>? Attachments { get; set; }
    public bool RequiresApproval { get; set; }
}