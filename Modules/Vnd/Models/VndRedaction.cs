using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Vnd.Models;

public class VndRedaction : IAuditableEntity
{
    public int Id { get; set; }
    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    public required string DocRu { get; set; }
    public string? DocKg { get; set; }
    public string? DocEn { get; set; }

    // Пока без отдельного модуля вложений — храним id файлов как массив (аналог attachmentIds)
    public int[] AttachmentIds { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}