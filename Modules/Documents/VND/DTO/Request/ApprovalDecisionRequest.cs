namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class ApprovalDecisionRequest
{
    public required ApprovalDecisionType Decision { get; set; }

    /// <summary>Обязателен для ApproveWithComment и Reject</summary>
    public string? Comment { get; set; }
}

public enum ApprovalDecisionType
{
    Approve = 0,
    ApproveWithComment = 1,
    Reject = 2
}