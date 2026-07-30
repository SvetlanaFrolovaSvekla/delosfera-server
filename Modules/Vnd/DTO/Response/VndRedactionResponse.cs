namespace delosfera_server.Modules.Vnd.DTO.Response;

public class VndRedactionResponse
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public int Number { get; set; }
    
    // TODO: будет ли это лучшим решением при переходе ВНД из черновика в зависимости от редакции?
    /* public required string VndStatus { get; set; }
    // Статус самой ВНД (изменяется в зависимости от редакции)*/
    
    public bool IsCurrent { get; set; } // Является ли последней актуальной
    
    public string? Description { get; set; }

    public int DocFileRuId { get; set; }
    public int? DocFileKgId { get; set; }
    public int? DocFileEnId { get; set; }

    public bool RequiresApproval { get; set; }
    public required string ApprovalStatus { get; set; }

    public List<int> AttachmentFileIds { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}