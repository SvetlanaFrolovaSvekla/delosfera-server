namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndProposalResponse
{
    public int Id { get; set; }

    public int VndId { get; set; }
    public string VndCode { get; set; } = "";
    public string VndTitle { get; set; } = "";

    public int? RedactionId { get; set; }
    public string? RedactionCode { get; set; }

    public string Text { get; set; } = "";
    public List<VndProposalQuoteResponse> Quotes { get; set; } = [];
    public List<VndProposalAttachmentResponse> Attachments { get; set; } = [];

    public int AuthorUserId { get; set; }
    public string AuthorName { get; set; } = "";
    public string? AuthorPosition { get; set; }
    public string? AuthorOrgUnit { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ReadByName { get; set; }
}

public class VndProposalQuoteResponse
{
    public string DocumentTarget { get; set; } = "ru";
    public string Text { get; set; } = "";
    public string? Note { get; set; }
}

public class VndProposalAttachmentResponse
{
    public int FileId { get; set; }
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
}

public class VndProposalPagedResponse
{
    public List<VndProposalResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class VndProposalCountsResponse
{
    public int Total { get; set; }
    public int Unread { get; set; }
}

/// <summary>Короткий ответ на отправку — полное представление автору не нужно.</summary>
public class VndProposalCreatedResponse
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}
