namespace delosfera_server.Modules.Users.DTO.Response;

/// <summary>Одно действие пользователя в ленте активности.</summary>
public class UserActivityItemResponse
{
    /// <summary>Тип: "vnd_created" | "approval_decided" | "approval_initiated".</summary>
    public required string Type { get; set; }
    public int? VndId { get; set; }
    public string? VndCode { get; set; }
    public string? VndTitle { get; set; }
    public required DateTime Timestamp { get; set; }
    public required string Description { get; set; }
}

/// <summary>Сводка активности пользователя: счётчики + последние действия.</summary>
public class UserActivityResponse
{
    public int VndCreatedCount { get; set; }
    public int ApprovalsDecidedCount { get; set; }
    public int ApprovalsInitiatedCount { get; set; }
    public List<UserActivityItemResponse> Recent { get; set; } = [];
}
