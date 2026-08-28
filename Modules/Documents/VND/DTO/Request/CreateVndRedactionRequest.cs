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
    
    /// <summary>Новые файлы вложений (то, что реально нужно загрузить в хранилище). Если по
    /// содержимому (SHA-256) файл совпадает с уже приложенным где-то в редакциях этого же ВНД -
    /// он не грузится повторно, переиспользуется существующий FileAttachmentId (см.
    /// VndService.AddRedactionAsync).</summary>
    public List<IFormFile>? Attachments { get; set; }

    /// <summary>Id уже существующих файлов вложений (из предыдущей редакции этого же ВНД),
    /// которые нужно перенести в новую редакцию как есть, без повторной загрузки - см. блок
    /// "Вложения" в VndUploadRedactionModal (предзаполняется вложениями последней редакции).
    /// Каждый id обязан принадлежать вложению одной из существующих редакций ЭТОГО ВНД -
    /// иначе перенос игнорируется (см. проверку в AddRedactionAsync).</summary>
    public List<int>? ExistingAttachmentFileIds { get; set; }

    public bool RequiresApproval { get; set; }
}