namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class ApprovalDecisionRequest
{
    public required ApprovalDecisionType Decision { get; set; }

    /// <summary>Обязателен для ApproveWithComment и Reject</summary>
    public string? Comment { get; set; }

    /// <summary>Файлы, приложенные согласующим к своей резолюции (необязательно).
    /// Хранятся, пока идёт согласование — как только редакция становится согласованной,
    /// вложения физически удаляются, а текст комментария остаётся.</summary>
    public List<IFormFile>? Files { get; set; }
}

public enum ApprovalDecisionType
{
    Approve = 0,
    ApproveWithComment = 1,
    Reject = 2
}