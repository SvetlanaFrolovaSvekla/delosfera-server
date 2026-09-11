using delosfera_server.Modules.Integrations.Mail;
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

    /// <summary>Вложение к письму (не к внутреннему уведомлению — там файлов нет). Например,
    /// Excel-план актуализации к ежемесячной сводке (ActualizationNotificationService).
    /// Игнорируется, если SkipEmail = true.</summary>
    public MailAttachment? Attachment { get; set; }

    /// <summary>
    /// Уже сохранённый в системе файл (см. IFileStorageService.SaveGeneratedAsync) — в отличие
    /// от Attachment выше, виден получателю прямо в карточке уведомления внутри Делосферы (см.
    /// Notification.AttachmentFileId), без почты. Например, Excel-план актуализации к
    /// единоразовой рассылке (см. ActualizationNotificationService.SendOneTimeMailingAsync).
    /// </summary>
    public int? AttachmentFileId { get; set; }

    /// <summary>Не ставить копию в очередь на корпоративную почту (INT-02) — только системное
    /// уведомление внутри Делосферы. По умолчанию false: поведение существующих вызовов не
    /// меняется.</summary>
    public bool SkipEmail { get; set; }
}
