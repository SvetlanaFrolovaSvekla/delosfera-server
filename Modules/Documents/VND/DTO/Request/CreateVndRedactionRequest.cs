using System.ComponentModel.DataAnnotations;
using delosfera_server.Common.Validation;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class CreateVndRedactionRequest
{
    [Required(ErrorMessage = "Файл документа (RU) обязателен")]
    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public required IFormFile DocRu { get; set; }

    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? DocKg { get; set; }

    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? DocEn { get; set; }

    /// <summary>Таблица изменений и дополнений (ТИД). Обязателен, если у ВНД уже есть предыдущая
    /// редакция — то есть документ актуализируется, а не создаётся впервые. Для самой первой
    /// редакции нового ВНД не требуется.</summary>
    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? Tid { get; set; }
    
    [StringLength(2000)]
    public string? Description { get; set; }

    [MaxLength(10, ErrorMessage = "Не более 10 вложений")]
    public List<IFormFile>? Attachments { get; set; }

    public bool RequiresApproval { get; set; }
}