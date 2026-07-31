namespace delosfera_server.Modules.Vnd.Notifications;

using delosfera_server.Modules.Notifications.Models;

/// <summary>Готовый набор заголовок+текст на трёх языках + серьёзность для одного уведомления</summary>
public record NotificationText(
    string TitleRu, string? TitleEn, string? TitleKg,
    string BodyRu, string? BodyEn, string? BodyKg,
    NotificationSeverity Severity);

// TODO: на английском нужно будет отобразить ФИО пользователя латинскими буками (фронт)

/// <summary>
/// Тексты уведомлений по событиям согласования редакций ВНД.
/// Динамические вставки (ФИО согласующего, комментарий) не переводятся,
/// приходят как есть от пользователя на любом языке.
/// </summary>
public static class VndApprovalNotificationMessages
{
    public static NotificationText TaskPrimaryApproval(string redactionCode, string vndTitle) => new(
        TitleRu: "Новая задача на согласование",
        TitleEn: "New approval task",
        TitleKg: "Макулдашуу боюнча жаңы тапшырма",
        BodyRu: $"Вам необходимо согласовать редакцию {redactionCode} документа «{vndTitle}» (первичное согласование).",
        BodyEn: $"You need to approve revision {redactionCode} of the document \"{vndTitle}\" (primary approval).",
        BodyKg: $"«{vndTitle}» документинин {redactionCode} редакциясын макулдашууңуз керек (биринчи макулдашуу).",
        Severity: NotificationSeverity.Urgent); // есть задача, требующая действия

    public static NotificationText TaskRepeatApproval(string redactionCode, string vndTitle) => new(
        TitleRu: "Повторное согласование",
        TitleEn: "Repeated approval",
        TitleKg: "Кайталап макулдашуу",
        BodyRu: $"Инициатор внёс исправления по редакции {redactionCode} документа «{vndTitle}». " +
                "Требуется повторное согласование с учётом ваших замечаний.",
        BodyEn: $"The initiator made corrections to revision {redactionCode} of the document \"{vndTitle}\". " +
                "Repeated approval is required, taking your remarks into account.",
        BodyKg: $"Демилгечи «{vndTitle}» документинин {redactionCode} редакциясына оңдоолорду киргизди. " +
                "Сиздин эскертүүлөрүңүздү эске алуу менен кайра макулдашуу талап кылынат.",
        Severity: NotificationSeverity.Urgent); // тоже задача, требующая действия

    public static NotificationText FinalHoldForApprovers(string redactionCode, string vndTitle) => new(
        TitleRu: "Редакция на финальной выдержке",
        TitleEn: "Revision in final hold",
        TitleKg: "Редакция акыркы кармоодо",
        BodyRu: $"Редакция {redactionCode} документа «{vndTitle}» прошла повторное согласование и находится " +
                "на финальной выдержке (ознакомление, без решений).",
        BodyEn: $"Revision {redactionCode} of the document \"{vndTitle}\" passed repeated approval and is now " +
                "in the final hold period (for review only, no decisions required).",
        BodyKg: $"«{vndTitle}» документинин {redactionCode} редакциясы кайра макулдашуудан өттү жана " +
                "акыркы кармоодо турат (таанышуу үчүн, чечим талап кылынбайт).",
        Severity: NotificationSeverity.Info); // просто ознакомление, действие не требуется

    public static NotificationText ApprovedByUser(string approverName, string redactionCode, string vndTitle) => new(
        TitleRu: "Редакцию согласовали",
        TitleEn: "Revision approved",
        TitleKg: "Редакция макулдашылды",
        BodyRu: $"{approverName} согласовал(а) редакцию {redactionCode} документа «{vndTitle}».",
        BodyEn: $"{approverName} approved revision {redactionCode} of the document \"{vndTitle}\".",
        BodyKg: $"{approverName} «{vndTitle}» документинин {redactionCode} редакциясын макулдады.",
        Severity: NotificationSeverity.Success);

    public static NotificationText ApprovedWithComment(
        string approverName, string redactionCode, string vndTitle, string? comment) => new(
        TitleRu: "Редакцию согласовали с замечаниями",
        TitleEn: "Revision approved with remarks",
        TitleKg: "Редакция эскертүүлөр менен макулдашылды",
        BodyRu: $"{approverName} согласовал(а) редакцию {redactionCode} документа «{vndTitle}» " +
                $"с замечаниями: «{comment}».",
        BodyEn: $"{approverName} approved revision {redactionCode} of the document \"{vndTitle}\" " +
                $"with remarks: \"{comment}\".",
        BodyKg: $"{approverName} «{vndTitle}» документинин {redactionCode} редакциясын эскертүүлөр менен " +
                $"макулдады: «{comment}».",
        Severity: NotificationSeverity.Warning); // формально согласовано, но есть замечания

    public static NotificationText Rejected(
        string approverName, string redactionCode, string vndTitle, string? comment) => new(
        TitleRu: "Редакцию отклонили",
        TitleEn: "Revision rejected",
        TitleKg: "Редакция четке кагылды",
        BodyRu: $"{approverName} отклонил(а) редакцию {redactionCode} документа «{vndTitle}». " +
                $"Причина: «{comment}».",
        BodyEn: $"{approverName} rejected revision {redactionCode} of the document \"{vndTitle}\". " +
                $"Reason: \"{comment}\".",
        BodyKg: $"{approverName} «{vndTitle}» документинин {redactionCode} редакциясын четке какты. " +
                $"Себеби: «{comment}».",
        Severity: NotificationSeverity.Urgent); // отклонение — требует немедленной реакции инициатора

    public static NotificationText ApprovedAfterRevision(string redactionCode, string vndTitle) => new(
        TitleRu: "Редакция согласована после доработки",
        TitleEn: "Revision approved after revision",
        TitleKg: "Редакция оңдоодон кийин макулдашылды",
        BodyRu: $"Редакция {redactionCode} документа «{vndTitle}» проверена после исправления замечаний " +
                "и согласована. Документ стал действующим.",
        BodyEn: $"Revision {redactionCode} of the document \"{vndTitle}\" was reviewed after remarks were " +
                "addressed and has been approved. The document is now active.",
        BodyKg: $"«{vndTitle}» документинин {redactionCode} редакциясы эскертүүлөр оңдолгондон кийин " +
                "текшерилип, макулдашылды. Документ колдонуудагы болду.",
        Severity: NotificationSeverity.Success);

    public static NotificationText Approved(string redactionCode, string vndTitle) => new(
        TitleRu: "Редакция согласована",
        TitleEn: "Revision approved",
        TitleKg: "Редакция макулдашылды",
        BodyRu: $"Редакция {redactionCode} документа «{vndTitle}» согласована. Документ стал действующим.",
        BodyEn: $"Revision {redactionCode} of the document \"{vndTitle}\" has been approved. " +
                "The document is now active.",
        BodyKg: $"«{vndTitle}» документинин {redactionCode} редакциясы макулдашылды. " +
                "Документ колдонуудагы болду.",
        Severity: NotificationSeverity.Success);

    public static NotificationText SentToFinalHold(string redactionCode) => new(
        TitleRu: "Редакция отправлена на финальную выдержку",
        TitleEn: "Revision sent to final hold",
        TitleKg: "Редакция акыркы кармоого жиберилди",
        BodyRu: $"Ваша редакция {redactionCode} прошла повторное согласование и отправлена на финальную выдержку.",
        BodyEn: $"Your revision {redactionCode} passed repeated approval and was sent to the final hold period.",
        BodyKg: $"Сиздин {redactionCode} редакцияңыз кайра макулдашуудан өтүп, акыркы кармоого жиберилди.",
        Severity: NotificationSeverity.Info);

    public static NotificationText SentToApproval(string redactionCode, string vndTitle) => new(
        TitleRu: "Редакция отправлена на согласование",
        TitleEn: "Revision sent for approval",
        TitleKg: "Редакция макулдашууга жиберилди",
        BodyRu: $"Ваша редакция {redactionCode} документа «{vndTitle}» отправлена на согласование.",
        BodyEn: $"Your revision {redactionCode} of the document \"{vndTitle}\" was sent for approval.",
        BodyKg: $"Сиздин «{vndTitle}» документинин {redactionCode} редакцияңыз макулдашууга жиберилди.",
        Severity: NotificationSeverity.Info);

    public static NotificationText RevisionNeeded(string redactionCode, string vndTitle) => new(
        TitleRu: "Редакция требует доработки",
        TitleEn: "Revision requires changes",
        TitleKg: "Редакция оңдоону талап кылат",
        BodyRu: $"По редакции {redactionCode} документа «{vndTitle}» есть замечания. " +
                "Внесите исправления и отправьте на повторное согласование.",
        BodyEn: $"There are remarks on revision {redactionCode} of the document \"{vndTitle}\". " +
                "Make the corrections and resubmit for approval.",
        BodyKg: $"«{vndTitle}» документинин {redactionCode} редакциясы боюнча эскертүүлөр бар. " +
                "Оңдоолорду киргизип, кайра макулдашууга жибериңиз.",
        Severity: NotificationSeverity.Warning); // требует доработки, но не критично
}