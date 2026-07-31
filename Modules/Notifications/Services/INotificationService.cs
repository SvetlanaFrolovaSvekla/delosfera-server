using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.DTO.Response;

namespace delosfera_server.Modules.Notifications.Services;

public interface INotificationService
{
    Task<int> CreateAsync(CreateNotificationRequest request, int? currentUserId);
    Task<PagedNotificationResponse> SearchAsync(NotificationFilterRequest request, int currentUserId, string languageCode);
    Task<NotificationResponse> GetByIdAsync(int userNotificationId, int currentUserId, string languageCode);
    Task<NotificationResponse> MarkAsReadAsync(int userNotificationId, int currentUserId, string languageCode);
    Task<NotificationResponse> MarkAsUnreadAsync(int userNotificationId, int currentUserId, string languageCode);
    Task<int> MarkAllAsReadAsync(int currentUserId, Models.NotificationCategory? category);
    Task<NotificationResponse> ToggleFavoriteAsync(int userNotificationId, int currentUserId, string languageCode);
    Task DeleteForUserAsync(int userNotificationId, int currentUserId);
    Task<NotificationCountsResponse> GetCountsAsync(int currentUserId);
}