namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>
/// Демонстрация письма ежемесячной сводки для одного СП — раздел "Уведомления" → "Настройки
/// рассылок" → "Нормотворчество" → "Ежемесячное уведомление". Показывает, что реально уйдёт
/// адресатам, если сводку отправить сейчас: тема, текст, имя вложения и получатели. Письмо не
/// отправляется — это только предпросмотр (см. ActualizationNotificationService.PreviewMonthlyDigestAsync).
/// </summary>
public class ActualizationNotificationPreviewResponse
{
    public int OrgUnitId { get; set; }
    public required string OrgUnitName { get; set; }

    public required string Subject { get; set; }
    public required string Body { get; set; }
    public required string AttachmentFileName { get; set; }

    public List<string> RecipientNames { get; set; } = [];

    public int TotalCount { get; set; }
    public int NormalCount { get; set; }
    public int ApproachingCount { get; set; }
    public int CriticalCount { get; set; }
    public int OverdueCount { get; set; }
}
