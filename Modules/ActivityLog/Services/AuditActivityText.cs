namespace delosfera_server.Modules.ActivityLog.Services;

/// <summary>
/// Человекочитаемое описание записи технического аудита.
///
/// Все контуры пишут в единый аудит (тип, id, действие, кто, когда), но журнал
/// действий показывал только ВНД — у него был свой отдельный поток с готовым
/// текстом. Здесь аудит превращается в строку журнала на лету, поэтому журнал
/// покрывает каждый контур, который вообще что-то пишет в аудит, без дописывания
/// вызовов в каждый сервис.
/// </summary>
public static class AuditActivityText
{
    /// <summary>Что за документ стоит за типом аудита — для заголовка строки и ссылки.</summary>
    public static (string Module, string UrlPrefix) Origin(string entityType) => entityType switch
    {
        "Sz" or "SzAssignment" => ("Служебная записка", "/sz/"),
        "ProcurementRequest" or "Tender" or "ProcurementProtocol" or "ProcurementContract"
            or "ProcurementClaim" or "Guarantee" => ("Закупка", "/prc/"),
        "ProcurementPlan" => ("План закупок", "/prc/plan/"),
        "Supplier" => ("Поставщик", "/prc/suppliers/"),
        "Document" or "DocumentAttachment" => ("Документ", "/documents/"),
        "AcknowledgementSheet" or "AcknowledgementEntry" => ("Ознакомление", "/acknowledgements/"),
        "Substitution" => ("Замещение", "/management/substitutions"),
        "RouteTemplate" or "RouteTemplateStep" or "RouteParticipant" or "Remark" =>
            ("Маршрут согласования", "/management/route-templates"),
        "SimpleSignatureRegulation" or "SigningSettings" or "TrustedCertificateAuthority"
            or "UserCertificate" => ("Подписание", "/management/signing"),
        "HelpArticle" => ("Справка", "/help"),
        "Directory" or "DirectorySettings" => ("Синхронизация", "/management/integrations"),
        "ProcurementParameter" => ("Параметры закупок", "/management/refs"),
        _ => (entityType, "/"),
    };

    /// <summary>
    /// Глагольное описание действия и значок. Ключ — «Тип.Действие», потому что
    /// одно и то же слово («Создан», «Обновлён») значит разное в разных контурах;
    /// на неизвестную пару отвечаем действием как есть, а не пустотой.
    /// </summary>
    public static (string Ru, string Icon) Describe(string entityType, string action) =>
        Map.TryGetValue($"{entityType}.{action}", out var exact) ? exact
        : Generic.TryGetValue(action, out var generic) ? generic
        : (action, "info");

    private static readonly Dictionary<string, (string Ru, string Icon)> Generic = new()
    {
        ["Created"] = ("создал(а)", "doc"),
        ["Updated"] = ("изменил(а)", "edit"),
        ["Deleted"] = ("удалил(а)", "x"),
        ["Completed"] = ("завершил(а)", "check"),
        ["Signed"] = ("подписал(а)", "check"),
        ["Registered"] = ("зарегистрировал(а)", "doc"),
        ["Withdrawn"] = ("отозвал(а)", "x"),
    };

    private static readonly Dictionary<string, (string Ru, string Icon)> Map = new()
    {
        // ── Служебная записка ────────────────────────────────────────────────
        ["Sz.Created"] = ("создал(а) записку", "doc"),
        ["Sz.Updated"] = ("изменил(а) записку", "edit"),
        ["Sz.Deleted"] = ("удалил(а) черновик записки", "x"),
        ["Sz.SubmittedForApproval"] = ("отправил(а) на согласование", "clock"),
        ["Sz.ApproversChanged"] = ("изменил(а) состав согласующих", "edit"),
        ["Sz.Resolution"] = ("вынес(ла) резолюцию", "check"),
        ["Sz.AddresseeDecided"] = ("принял(а) решение адресата", "check"),
        ["Sz.SubmittedToBody"] = ("вынес(ла) на коллегиальный орган", "clock"),
        ["Sz.Registered"] = ("зарегистрировал(а) записку", "doc"),
        ["Sz.SentToSigner"] = ("направил(а) на подписание", "clock"),
        ["Sz.Withdrawn"] = ("отозвал(а) записку", "x"),
        ["Sz.StatusForced"] = ("изменил(а) статус вручную", "edit"),
        ["Sz.StatusFromRoute"] = ("статус изменён по итогу маршрута", "check"),
        ["Sz.DueDateExtended"] = ("продлил(а) срок исполнения", "clock"),
        ["Sz.Executed"] = ("отметил(а) записку исполненной", "check"),
        ["Sz.HandedToProcurement"] = ("передал(а) в закупку", "clock"),
        ["Sz.SubmittedToBody "] = ("вынес(ла) на коллегиальный орган", "clock"),
        ["Sz.Archived"] = ("сдал(а) записку в архив", "doc"),
        ["Sz.RestoredFromArchive"] = ("вернул(а) записку из архива", "doc"),
        ["Sz.OriginalHandedOver"] = ("выдал(а) оригинал на руки", "doc"),
        ["Sz.OriginalReturned"] = ("принял(а) оригинал обратно", "doc"),
        ["SzAssignment.Reported"] = ("отчитался(ась) по поручению", "check"),
        ["SzAssignment.Accepted"] = ("принял(а) исполнение поручения", "check"),
        ["SzAssignment.Returned"] = ("вернул(а) поручение на доработку", "x"),
        ["SzAssignment.Cancelled"] = ("снял(а) поручение", "x"),

        // ── Закупка ──────────────────────────────────────────────────────────
        ["ProcurementRequest.Created"] = ("создал(а) заявку на закупку", "doc"),
        ["ProcurementRequest.Updated"] = ("изменил(а) заявку", "edit"),
        ["ProcurementRequest.Deleted"] = ("удалил(а) черновик заявки", "x"),
        ["ProcurementRequest.SubmittedForApproval"] = ("отправил(а) заявку на согласование", "clock"),
        ["ProcurementRequest.StatusFromRoute"] = ("статус заявки изменён по итогу маршрута", "check"),
        ["ProcurementRequest.Withdrawn"] = ("отозвал(а) заявку", "x"),
        ["ProcurementRequest.Completed"] = ("завершил(а) закупку по заявке", "check"),
        ["ProcurementRequest.WinnerDeclared"] = ("определил(а) победителя", "check"),
        ["ProcurementRequest.ProposalRegistered"] = ("зарегистрировал(а) предложение", "doc"),
        ["ProcurementRequest.ProposalRemoved"] = ("убрал(а) предложение", "x"),
        ["ProcurementRequest.ProposalSourcesChanged"] = ("изменил(а) источники предложений", "edit"),
        ["ProcurementRequest.ProposalVerdict"] = ("вынес(ла) вердикт по предложению", "check"),
        ["Tender.Created"] = ("создал(а) конкурс", "doc"),
        ["Tender.Published"] = ("опубликовал(а) конкурс", "clock"),
        ["Tender.PublicationConfirmed"] = ("подтвердил(а) публикацию", "check"),
        ["Tender.BidRegistered"] = ("зарегистрировал(а) конкурсную заявку", "doc"),
        ["Tender.BidsOpened"] = ("вскрыл(а) конкурсные заявки", "doc"),
        ["Tender.BidScored"] = ("оценил(а) заявку", "check"),
        ["Tender.AttendanceMarked"] = ("отметил(а) явку комиссии", "check"),
        ["Tender.VotesRecorded"] = ("внёс(ла) голоса комиссии", "check"),
        ["Tender.ExpertConclusion"] = ("приложил(а) экспертное заключение", "doc"),
        ["Tender.CommissionMemberAdded"] = ("добавил(а) члена комиссии", "edit"),
        ["Tender.CommissionMemberRemoved"] = ("вывел(а) члена комиссии", "edit"),
        ["Tender.WinnerDeclared"] = ("объявил(а) победителя конкурса", "check"),
        ["ProcurementProtocol.Updated"] = ("изменил(а) протокол", "edit"),
        ["ProcurementProtocol.Signed"] = ("подписал(а) протокол", "check"),
        ["ProcurementContract.Created"] = ("создал(а) договор", "doc"),
        ["ProcurementContract.Updated"] = ("изменил(а) договор", "edit"),
        ["ProcurementContract.ActRegistered"] = ("зарегистрировал(а) акт приёмки", "doc"),
        ["ProcurementContract.ToppedUp"] = ("увеличил(а) сумму договора", "edit"),
        ["ProcurementContract.Completed"] = ("закрыл(а) договор", "check"),
        ["ProcurementContract.Terminated"] = ("расторг(ла) договор", "x"),
        ["ProcurementClaim.Created"] = ("создал(а) претензию", "doc"),
        ["ProcurementClaim.Sent"] = ("направил(а) претензию", "clock"),
        ["ProcurementClaim.Answered"] = ("получил(а) ответ на претензию", "check"),
        ["ProcurementClaim.Closed"] = ("закрыл(а) претензию", "check"),
        ["Guarantee.Received"] = ("принял(а) гарантию", "doc"),
        ["ProcurementPlan.Created"] = ("создал(а) план закупок", "doc"),
        ["ProcurementPlan.Approved"] = ("утвердил(а) план закупок", "check"),
        ["ProcurementPlan.ItemAdded"] = ("добавил(а) позицию плана", "edit"),
        ["ProcurementPlan.ItemRemoved"] = ("убрал(а) позицию плана", "edit"),
        ["Supplier.Blacklisted"] = ("внёс(ла) поставщика в чёрный список", "x"),
        ["Supplier.RemovedFromBlacklist"] = ("убрал(а) поставщика из чёрного списка", "check"),
        ["Supplier.ReliabilityChecked"] = ("проверил(а) надёжность поставщика", "check"),
        ["ProcurementParameter.Updated"] = ("изменил(а) параметры закупок", "edit"),

        // ── Ознакомление ─────────────────────────────────────────────────────
        ["AcknowledgementSheet.Created"] = ("завёл(ла) лист ознакомления", "doc"),
        ["AcknowledgementSheet.ParticipantsAdded"] = ("добавил(а) ознакомляемых", "edit"),
        ["AcknowledgementSheet.Closed"] = ("закрыл(а) лист ознакомления", "check"),
        ["AcknowledgementEntry.Acknowledged"] = ("ознакомился(ась)", "check"),
        ["AcknowledgementEntry.Refused"] = ("отказался(ась) от ознакомления", "x"),
        ["AcknowledgementEntry.Cancelled"] = ("снят(а) с ознакомления", "x"),

        // ── Документ и подпись ───────────────────────────────────────────────
        ["Document.Signed"] = ("подписал(а) документ", "check"),
        ["DocumentAttachment.Added"] = ("приложил(а) файл", "doc"),
        ["DocumentAttachment.Replaced"] = ("заменил(а) файл", "edit"),
        ["DocumentAttachment.Deleted"] = ("удалил(а) файл", "x"),
        ["DocumentAttachment.Signed"] = ("подписал(а) вложение", "check"),
        ["DocumentAttachment.SignaturesRevoked"] = ("отозвал(а) подписи вложения", "x"),

        // ── Маршрут ──────────────────────────────────────────────────────────
        ["RouteTemplate.Updated"] = ("изменил(а) маршрут согласования", "edit"),
        ["RouteTemplate.Deleted"] = ("удалил(а) маршрут согласования", "x"),
        ["RouteTemplateStep.SignatureLevelChanged"] = ("изменил(а) уровень подписи этапа", "edit"),
        ["RouteParticipant.EscalatedFinalControl"] = ("эскалировал(а) на финальный контроль", "clock"),
        ["RouteParticipant.AutoAcceptSuppressedByRemarks"] =
            ("автоакцепт отменён из-за замечаний", "info"),
        ["Remark.Resolved"] = ("снял(а) замечание", "check"),

        // ── Замещение, подписание, справка, синхронизация ────────────────────
        ["Substitution.Created"] = ("оформил(а) замещение", "doc"),
        ["Substitution.Cancelled"] = ("отменил(а) замещение", "x"),
        ["SimpleSignatureRegulation.Accepted"] = ("принял(а) регламент подписи", "check"),
        ["SigningSettings.Updated"] = ("изменил(а) настройки подписания", "edit"),
        ["TrustedCertificateAuthority.Added"] = ("добавил(а) доверенный УЦ", "edit"),
        ["TrustedCertificateAuthority.Enabled"] = ("включил(а) доверенный УЦ", "check"),
        ["TrustedCertificateAuthority.Disabled"] = ("отключил(а) доверенный УЦ", "x"),
        ["UserCertificate.Revoked"] = ("отозвал(а) сертификат", "x"),
        ["HelpArticle.Created"] = ("создал(а) статью справки", "doc"),
        ["HelpArticle.Updated"] = ("изменил(а) статью справки", "edit"),
        ["HelpArticle.Deleted"] = ("удалил(а) статью справки", "x"),
        ["Directory.Synced"] = ("синхронизировал(а) справочник", "check"),
        ["DirectorySettings.Updated"] = ("изменил(а) настройки синхронизации", "edit"),
    };
}
