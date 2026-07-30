namespace delosfera_server.Modules.Vnd.DTO.Request;

public class CreateVndRedactionRequest
{
    public required IFormFile DocRu { get; set; }
    public IFormFile? DocKg { get; set; }
    public IFormFile? DocEn { get; set; }
    
    public string? Description { get; set; }
    
    public List<IFormFile>? Attachments { get; set; }
    public bool RequiresApproval { get; set; }
}