namespace delosfera_server.Modules.Vnd.Notifications;

using delosfera_server.Modules.Notifications.Models;

/// <summary>Тексты уведомлений по заявкам и событиям актуализации ВНД.
/// NotificationText — тот же record, что используется в VndApprovalNotificationMessages.</summary>
public static class VndActualizationNotificationMessages
{
    public static NotificationText AccessRequested(string vndTitle, string requesterName) => new(
        TitleRu: "Заявка на доступ к актуализации",
        TitleEn: "Actualization access request",
        TitleKg: "Актуалдаштырууга кирүү укугуна арыз",
        BodyRu: $"{requesterName} запросил(а) доступ к актуализации документа «{vndTitle}».",
        BodyEn: $"{requesterName} requested access to actualize the document \"{vndTitle}\".",
        BodyKg: $"{requesterName} «{vndTitle}» документин актуалдаштырууга уруксат сурады.",
        Severity: NotificationSeverity.Urgent);

    public static NotificationText AccessApproved(string vndTitle) => new(
        TitleRu: "Доступ к актуализации одобрен",
        TitleEn: "Actualization access approved",
        TitleKg: "Актуалдаштырууга кирүү уруксат берилди",
        BodyRu: $"Ваша заявка на актуализацию документа «{vndTitle}» одобрена. Можно начинать процесс.",
        BodyEn: $"Your request to actualize the document \"{vndTitle}\" was approved. You can start the process.",
        BodyKg: $"«{vndTitle}» документин актуалдаштырууга болгон арызыңыз бекитилди. Процессти баштасаңыз болот.",
        Severity: NotificationSeverity.Success);

    public static NotificationText AccessRejected(string vndTitle) => new(
        TitleRu: "Доступ к актуализации отклонён",
        TitleEn: "Actualization access rejected",
        TitleKg: "Актуалдаштырууга кирүү четке кагылды",
        BodyRu: $"Ваша заявка на актуализацию документа «{vndTitle}» отклонена.",
        BodyEn: $"Your request to actualize the document \"{vndTitle}\" was rejected.",
        BodyKg: $"«{vndTitle}» документин актуалдаштырууга болгон арызыңыз четке кагылды.",
        Severity: NotificationSeverity.Warning);

    public static NotificationText Published(string vndTitle, bool hadChanges) => new(
        TitleRu: "ВНД опубликован после актуализации",
        TitleEn: "VND published after actualization",
        TitleKg: "ВНД актуалдаштыруудан кийин жарыяланды",
        BodyRu: $"Документ «{vndTitle}» опубликован после актуализации" +
                (hadChanges ? " (с изменениями)." : " (без изменений)."),
        BodyEn: $"The document \"{vndTitle}\" was published after actualization" +
                (hadChanges ? " (with changes)." : " (without changes)."),
        BodyKg: $"«{vndTitle}» документи актуалдаштыруудан кийин жарыяланды" +
                (hadChanges ? " (өзгөртүүлөр менен)." : " (өзгөртүүсүз)."),
        Severity: NotificationSeverity.Success);
}