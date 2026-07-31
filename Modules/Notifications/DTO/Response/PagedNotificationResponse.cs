namespace delosfera_server.Modules.Notifications.DTO.Response;

public class PagedNotificationResponse
{
    public required List<NotificationResponse> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}