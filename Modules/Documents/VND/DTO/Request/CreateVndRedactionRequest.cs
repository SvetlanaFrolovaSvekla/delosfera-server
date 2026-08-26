namespace delosfera_server.Modules.Documents.VND.DTO.Request;
using System.ComponentModel.DataAnnotations;

public class CreateVndRedactionRequest
{
    public required IFormFile DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }

    /// <summary>Таблица изменений и дополнений (ТИД). Обязателен, если у ВНД уже есть предыдущая
    /// редакция — то есть документ актуализируется, а не создаётся впервые. Для самой первой
    /// редакции нового ВНД не требуется.</summary>
    public IFormFile? Tid { get; set; }
    
    [StringLength(500, ErrorMessage = "Описание редакции не может превышать 500 символов!")]
    public string? Description { get; set; }
    
    public List<IFormFile>? Attachments { get; set; }
    public bool RequiresApproval { get; set; }
}