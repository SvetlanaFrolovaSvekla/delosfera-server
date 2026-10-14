namespace delosfera_server.Modules.ActivityLog.DTO.Response;

/// <summary>Страница ленты "Активность на портале" (см. ActivityFeedRequest).</summary>
public class ActivityFeedResponse
{
    public List<ActivityLogEntryResponse> Items { get; set; } = [];

    /// <summary>Есть ли записи дальше — показывать ли "Показать ещё".</summary>
    public bool HasMore { get; set; }

    /// <summary>Передать в Cursor, чтобы получить следующую страницу.</summary>
    public string? NextCursor { get; set; }
}
