using delosfera_server.Modules.Notifications.Models;

namespace delosfera_server.Modules.Documents.VND.Messages;

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

    /// <summary>Заявка одобрена через DecideRequestAsync — там же, в момент одобрения, главный
    /// редактор решает и сдвиг срока следующей актуализации (заявитель это не выбирает и
    /// никакого "пожелания" не подаёт), поэтому сразу сообщаем финальное решение. Отдельно от
    /// AccessApproved(vndTitle) выше — тот используется при авто-одобрении заявки в StartAsync
    /// (прямой старт главным редактором), где сдвиг срока ещё не решён на этом шаге (решается
    /// позже, на шаге "Выполнить актуализацию"), и апеллировать к нему рано.</summary>
    public static NotificationText AccessApprovedWithShiftDecision(string vndTitle, bool shiftNextPeriod) => new(
        TitleRu: "Доступ к актуализации одобрен",
        TitleEn: "Actualization access approved",
        TitleKg: "Актуалдаштырууга кирүү уруксат берилди",
        BodyRu: $"Ваша заявка на актуализацию документа «{vndTitle}» одобрена. Можно начинать процесс. " +
                "Срок следующей актуализации после публикации " +
                (shiftNextPeriod ? "будет сдвинут." : "сдвигаться не будет."),
        BodyEn: $"Your request to actualize the document \"{vndTitle}\" was approved. You can start the process. " +
                "The next actualization due date after publishing " +
                (shiftNextPeriod ? "will be shifted." : "will not be shifted."),
        BodyKg: $"«{vndTitle}» документин актуалдаштырууга болгон арызыңыз бекитилди. Процессти баштасаңыз болот. " +
                "Жарыялангандан кийин кийинки актуалдаштыруу мөөнөтү " +
                (shiftNextPeriod ? "жылдырылат." : "жылдырылбайт."),
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

    /// <summary>Заявка автоматически отклонена, потому что по этому же ВНД была одобрена другая
    /// заявка (на актуализацию можно взять только одного ответственного за раз). Без отдельного
    /// комментария от главного редактора — просто фиксируем факт.</summary>
    public static NotificationText AccessRejectedAnotherApproved(string vndTitle) => new(
        TitleRu: "Доступ к актуализации отклонён",
        TitleEn: "Actualization access rejected",
        TitleKg: "Актуалдаштырууга кирүү четке кагылды",
        BodyRu: $"Ваша заявка на актуализацию документа «{vndTitle}» отклонена: по этому документу " +
                "уже одобрена заявка другого пользователя.",
        BodyEn: $"Your request to actualize the document \"{vndTitle}\" was rejected: another user's " +
                "request for this document has already been approved.",
        BodyKg: $"«{vndTitle}» документин актуалдаштырууга болгон арызыңыз четке кагылды: бул документ " +
                "боюнча башка колдонуучунун арызы буга чейин эле бекитилген.",
        Severity: NotificationSeverity.Warning);

    /// <summary>Главный редактор назначил кого-то ДРУГОГО ответственным за актуализацию при
    /// прямом старте цикла (StartAsync) — тот должен узнать, что ему нужно выполнить шаг
    /// "Выполнить актуализацию". Себе самому (назначил себя же) уведомление не шлём.</summary>
    public static NotificationText AssignedResponsible(string vndTitle, string assignedByName) => new(
        TitleRu: "Вы назначены ответственным за актуализацию",
        TitleEn: "You were assigned as actualization responsible",
        TitleKg: "Сиз актуалдаштырууга жооптуу дайындалдыңыз",
        BodyRu: $"{assignedByName} назначил(а) вас ответственным за актуализацию документа «{vndTitle}». " +
                "Перейдите во вкладку «Актуализация» и выполните шаг «Выполнить актуализацию».",
        BodyEn: $"{assignedByName} assigned you as responsible for actualizing the document \"{vndTitle}\". " +
                "Go to the \"Actualization\" tab and complete the \"Perform actualization\" step.",
        BodyKg: $"{assignedByName} сизди «{vndTitle}» документин актуалдаштырууга жооптуу кылып дайындады. " +
                "«Актуализация» бөлүмүнө өтүп, «Актуализацияны аткаруу» кадамын аткарыңыз.",
        Severity: NotificationSeverity.Urgent);

    public static NotificationText AccessRejectedDirectStart(string vndTitle, bool startedByChiefEditorThemself) => new(
        TitleRu: "Доступ к актуализации отклонён",
        TitleEn: "Actualization access rejected",
        TitleKg: "Актуалдаштырууга кирүү четке кагылды",
        BodyRu: $"Ваша заявка на актуализацию документа «{vndTitle}» отклонена: " +
                (startedByChiefEditorThemself
                    ? "актуализацию начал главный редактор ВНД."
                    : "ответственным за актуализацию назначен другой пользователь."),
        BodyEn: $"Your request to actualize the document \"{vndTitle}\" was rejected: " +
                (startedByChiefEditorThemself
                    ? "the actualization was started by the chief VND editor."
                    : "another user was assigned as responsible for the actualization."),
        BodyKg: $"«{vndTitle}» документин актуалдаштырууга болгон арызыңыз четке кагылды: " +
                (startedByChiefEditorThemself
                    ? "актуалдаштырууну ВНДдин башкы редактору баштады."
                    : "актуалдаштырууга башка колдонуучу дайындалды."),
        Severity: NotificationSeverity.Warning);
}