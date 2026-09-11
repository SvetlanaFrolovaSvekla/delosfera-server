namespace delosfera_server.Modules.Notifications.DTO.Response;

public class NotificationResponse
{
    /// <summary>Id записи UserNotification - используется для read/unread/favorite/delete операций</summary>
    public int Id { get; set; }

    public int NotificationId { get; set; }

    public required string Title { get; set; }
    public required string Body { get; set; }

    public required string Category { get; set; }

    public required string Severity { get; set; }

    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Url { get; set; }

    /// <summary>Файл, приложенный к уведомлению (см. Notification.AttachmentFileId) — скачивается
    /// через GET /api/files/{id}, доступ только получателям уведомления (см.
    /// VndFileAccessAuthorizer). Null, если к уведомлению ничего не приложено.</summary>
    public int? AttachmentFileId { get; set; }
    public string? AttachmentFileName { get; set; }

    public int? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public bool IsFavorite { get; set; }
    public DateTime? FavoritedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
