namespace delosfera_server.Modules.Notifications.DTO.Response;

public class NotificationCountsResponse
{
    public int TotalUnread { get; set; }
    public int TotalFavorites { get; set; }

    /// <summary>Ключ - код категории (int as string)</summary>
    public Dictionary<string, int> UnreadByCategory { get; set; } = [];
}