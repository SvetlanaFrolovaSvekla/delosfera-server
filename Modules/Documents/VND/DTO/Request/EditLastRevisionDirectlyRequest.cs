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

    /// <summary>Убрать документ на кыргызском языке без замены. Игнорируется, если одновременно
    /// передан DocKg — замена имеет приоритет. Русский документ обязателен и убрать его нельзя.</summary>
    public bool RemoveDocKg { get; set; }

    /// <summary>Убрать документ на английском языке без замены. Игнорируется, если одновременно
    /// передан DocEn — замена имеет приоритет.</summary>
    public bool RemoveDocEn { get; set; }

    [StringLength(500, ErrorMessage = "Описание редакции не может превышать 500 символов!")]
    public string? Description { get; set; }

    /// <summary>Новые вложения, добавляемые к редакции (см. тот же паттерн в
    /// ResubmitAfterRevisionRequest).</summary>
    public List<IFormFile>? NewAttachments { get; set; }

    /// <summary>Id файлов существующих вложений редакции, которые нужно удалить.</summary>
    public List<int>? RemovedAttachmentFileIds { get; set; }
}