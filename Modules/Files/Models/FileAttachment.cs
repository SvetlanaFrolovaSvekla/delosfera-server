using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Files.Models;

public class FileAttachment : IAuditableEntity
{
    public int Id { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string StorageKey { get; set; } // object name в бакете
    public string Bucket { get; set; } = "delosfera-vnd";
    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}