using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Notifications.Models;

/// <summary>Персональное состояние уведомления для конкретного получателя</summary>
public class UserNotification
{
    public int Id { get; set; }

    public int NotificationId { get; set; }
    public Notification? Notification { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; } // Время прочтения

    public bool IsFavorite { get; set; } // Избранное 
    public DateTime? FavoritedAt { get; set; } // Время добавления в избранное

    /// <summary>Скрыто ли уведомление из личного списка пользователя (мягкое удаление)</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Момент доставки уведомления этому пользователю</summary>
    public DateTime CreatedAt { get; set; }
}