namespace delosfera_server.Modules.ActivityLog.DTO.Response;

public class ActivityLogEntryResponse
{
    public int Id { get; set; }
    public required string Module { get; set; }
    public int EntityId { get; set; }
    public required string EntityCode { get; set; }

    /// <summary>"check" | "x" | "doc" | "clock" | "info" - иконка типа действия</summary>
    public required string Icon { get; set; }

    public required string Text { get; set; }
    public required string Url { get; set; }
    public DateTime CreatedAt { get; set; }
}