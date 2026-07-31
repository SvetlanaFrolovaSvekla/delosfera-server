using delosfera_server.Modules.Notifications.Models;

namespace delosfera_server.Modules.Notifications.DTO.Request;

public class CreateNotificationRequest
{
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public required string BodyRu { get; set; }
    public string? BodyEn { get; set; }
    public string? BodyKg { get; set; }

    public required NotificationCategory Category { get; set; }
    
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Url { get; set; }

    /// <summary>Конкретные получатели. Игнорируется, если ToAllUsers = true</summary>
    public List<int> UserIds { get; set; } = [];

    /// <summary>Разослать всем активным пользователям системы</summary>
    public bool ToAllUsers { get; set; }
}