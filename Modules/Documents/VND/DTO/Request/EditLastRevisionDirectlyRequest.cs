using delosfera_server.Common.Validation;

namespace delosfera_server.Modules.Documents.VND.DTO.Request;
using System.ComponentModel.DataAnnotations;

/// <summary>Прямое редактирование последней редакции ВНД (для EditLastRevisionDirectly) -
/// подмена файлов и/или описания без запуска согласования, без создания новой редакции
/// и без изменения даты актуализации.</summary>

public class EditLastRevisionDirectlyRequest
{
    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? DocRu { get; set; }

    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? DocKg { get; set; }

    [AllowedExtensions(".doc", ".docx")]
    [MaxFileSize(50)]
    public IFormFile? DocEn { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }
}