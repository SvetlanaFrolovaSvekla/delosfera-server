namespace delosfera_server.Modules.Documents.DTO.Response;

public class DocumentResponse
{
    public int Id { get; set; }
    public required string Type { get; set; }
    public string? RegNumber { get; set; }
    public required string Title { get; set; }
    public required string StatusCode { get; set; }
    public int AuthorId { get; set; }
    public int? CurrentRouteInstanceId { get; set; }
    public bool IsPaperCarrier { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DocumentAttachmentResponse> Attachments { get; set; } = [];
}

public class DocumentAttachmentResponse
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public required string Hash { get; set; }
    public long Size { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditEntryResponse
{
    public long Id { get; set; }
    public required string Action { get; set; }
    public int? UserId { get; set; }
    public DateTime At { get; set; }
    public string? PayloadJson { get; set; }
}
