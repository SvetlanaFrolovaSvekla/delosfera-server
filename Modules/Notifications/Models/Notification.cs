using delosfera_server.Common.Models;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Notifications.Models;

public class Notification : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public required string BodyRu { get; set; }
    public string? BodyEn { get; set; }
    public string? BodyKg { get; set; }

    public NotificationCategory Category { get; set; }

    public NotificationSeverity Severity { get; set; }

    /// <summary>Тип связанной сущности для навигации на фронте, например "Vnd", "VndApproval"</summary>
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }

    /// <summary>Готовый URL, чтоб не резолвить EntityType/EntityId</summary>
    public string? Url { get; set; }

    /// <summary>
    /// Файл, приложенный к уведомлению — виден получателю прямо в карточке уведомления (кнопка
    /// «Скачать» на OpenNotificationPage/в NotificationRow на фронте), без почты. Например,
    /// Excel-план актуализации к единоразовой рассылке (см.
    /// ActualizationNotificationService.SendOneTimeMailingAsync). Это НЕ то же самое, что
    /// MailAttachment (Integrations.Mail) — тот кладётся во вложение письма и виден только по
    /// почте; этот файл хранится в системе (см. IFileStorageService) и доступен через API файлов
    /// самим получателям уведомления (см. VndFileAccessAuthorizer).
    /// </summary>
    public int? AttachmentFileId { get; set; }
    public FileAttachment? AttachmentFile { get; set; }

    /// <summary>Кто инициировал уведомление. Будет Null - если сгенерировано системой</summary>
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    // Получатель уведомления
    public ICollection<UserNotification> Recipients { get; set; } = new List<UserNotification>();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
