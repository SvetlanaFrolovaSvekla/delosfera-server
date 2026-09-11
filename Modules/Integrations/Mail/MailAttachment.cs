namespace delosfera_server.Modules.Integrations.Mail;

/// <summary>
/// Вложение для письма из очереди (INT-02) — например, Excel-файл плана актуализации к
/// ежемесячной сводке (см. ActualizationNotificationService). Необязательный параметр
/// IMailQueue.EnqueueAsync: подавляющее большинство уведомлений вложений не несёт.
/// </summary>
public class MailAttachment
{
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Bytes { get; set; }
}
