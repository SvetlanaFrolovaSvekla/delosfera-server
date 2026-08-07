namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndLinkResponse
{
    public int Id { get; set; }
    public int VndId { get; set; } // id документа на другом конце связи
    public required string Code { get; set; }
    public required string Title { get; set; }
    public required string Status { get; set; } // "active" | "onact" | "review" | "consol" | "arch" | "draft"
}

public class VndLinksResponse
{
    /// <summary>Ссылки на другие документы (этот документ - источник)</summary>
    public List<VndLinkResponse> Outgoing { get; set; } = [];

    /// <summary>Документы, ссылающиеся на этот (этот документ - цель)</summary>
    public List<VndLinkResponse> Incoming { get; set; } = [];
}