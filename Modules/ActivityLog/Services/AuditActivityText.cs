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
        // Отдельной карточки листа нет — ведём на общую страницу ознакомления (как taskLink на фронте).
        // Префикс без «/» на конце — ссылка без id (см. ActivityLogService.AuditToResponse).
        "AcknowledgementSheet" or "AcknowledgementEntry" => ("Ознакомление", "/hr-ack"),
        // Заявка на замещение (КСЗ-В9) — карточка /substitutions/{id}.
        "SubstitutionRequest" => ("Заявка на замещение", "/substitutions/"),
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
    /// на неизвестную пару отвечаем нейтральным «выполнил(а) действие», а не служебным
    /// кодом вроде «RouteChangedByAdmin» — код пользователю ничего не говорит. Новое
    /// действие в аудите = новая строка в Map ниже.
    /// </summary>
    public static (string Ru, string Icon) Describe(string entityType, string action) =>
        Map.TryGetValue($"{entityType}.{action}", out var exact) ? exact
        : Generic.TryGetValue(action, out var generic) ? generic
        : ("выполнил(а) действие", "info");

    /// <summary>
    /// Уточнение строки заявки на замещение по данным записи аудита: номер заявки, за кого
    /// действовал замещающий (GEN-14), этап маршрута, какой документ подписан. Без этого лента
    /// не различала «согласовал сам» и «согласовал за директора в отпуске».
    /// </summary>
    public static string DescribeSubstitution(string action, string baseRu, string number,
        string? payloadJson, Func<int, string?> nameOf)
    {
        int? Int(System.Text.Json.JsonElement root, string prop) =>
            TryGet(root, prop, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
        string? Str(System.Text.Json.JsonElement root, string prop) =>
            TryGet(root, prop, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;

        System.Text.Json.JsonElement p = default;
        var hasPayload = false;
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
                p = doc.RootElement.Clone();
                hasPayload = p.ValueKind == System.Text.Json.JsonValueKind.Object;
            }
            catch (System.Text.Json.JsonException)
            {
                // Повреждённый payload — строка без уточнений.
            }
        }

        var ru = baseRu;
        if (hasPayload && action is "Signed" or "SignedTest")
        {
            var docTitle = Str(p, "document") switch
            {
                "Order" => "приказ",
                "Liability" => "договор о материальной ответственности",
                _ => "документ",
            };
            var role = Str(p, "role") switch
            {
                "Director" => "как директор",
                "Absent" => "как отсутствующий (ознакомлен и согласен)",
                "Substitute" => "как замещающий",
                _ => null,
            };
            var version = Int(p, "version");
            ru = (action == "SignedTest" ? "подписал(а) тестовой имитацией ЭЦП " : "подписал(а) ЭЦП ")
                 + docTitle + (version is > 1 ? $" (версия {version})" : "")
                 + (role is null ? "" : $" {role}")
                 + " по заявке на замещение";
        }

        var text = $"{ru} {number}";
        if (!hasPayload) return text;

        if (action is "Approved" or "Rejected" or "StepReassigned" && Str(p, "step") is { } step)
            text += $" — этап «{step}»";
        if (action == "StepReassigned" && Int(p, "to") is { } to)
            text += $": {nameOf(Int(p, "from") ?? 0) ?? "—"} → {nameOf(to) ?? "—"}";
        if (action == "Withdrawn" && Str(p, "from") is "OnExecution")
            text += " (с этапа подписания)";
        if (Int(p, "onBehalfOf") is { } ob && nameOf(ob) is { } obName)
            text += $" за {obName} (по замещению)";
        return text;
    }

    private static bool TryGet(System.Text.Json.JsonElement root, string prop, out System.Text.Json.JsonElement value)
    {
        // Payload пишется анонимными объектами — регистр имён может отличаться (Version/version).
        foreach (var x in root.EnumerateObject())
            if (string.Equals(x.Name, prop, StringComparison.OrdinalIgnoreCase))
            {
                value = x.Value;
                return true;
            }
        value = default;
        return false;
    }

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
        ["Sz.SubmittedForRegistration"] = ("отправил(а) записку на регистрацию", "clock"),
        ["Sz.SubmittedForApproval"] = ("отправил(а) записку на согласование", "clock"),
        ["Sz.ResubmittedForApproval"] = ("повторно отправил(а) записку на согласование", "clock"),
        ["Sz.ApproversChanged"] = ("изменил(а) состав согласующих записки", "edit"),
        ["Sz.Resolution"] = ("вынес(ла) резолюцию по записке", "check"),
        ["Sz.AddresseeDecided"] = ("принял(а) решение адресата по записке", "check"),
        ["Sz.SubmittedToBody"] = ("вынес(ла) записку на коллегиальный орган", "clock"),
        ["Sz.WithdrawnFromBody"] = ("снял(а) записку с коллегиального органа", "x"),
        ["Sz.TrainingBudgetSet"] = ("указал(а) бюджет обучения в записке", "edit"),
        ["Sz.Registered"] = ("зарегистрировал(а) записку", "doc"),
        ["Sz.SentToSigner"] = ("направил(а) записку на подписание", "clock"),
        ["Sz.Withdrawn"] = ("отозвал(а) записку", "x"),
        ["Sz.StatusForced"] = ("изменил(а) статус записки вручную", "edit"),
        ["Sz.StatusFromRoute"] = ("изменил(а) статус записки по итогу маршрута", "check"),
        ["Sz.DueDateExtended"] = ("продлил(а) срок исполнения записки", "clock"),
        ["Sz.Executed"] = ("отметил(а) записку исполненной", "check"),
        ["Sz.HandedToProcurement"] = ("передал(а) записку в закупку", "clock"),
        ["Sz.SubmittedToBody "] = ("вынес(ла) записку на коллегиальный орган", "clock"),
        ["Sz.Archived"] = ("сдал(а) записку в архив", "doc"),
        ["Sz.RestoredFromArchive"] = ("вернул(а) записку из архива", "doc"),
        ["Sz.OriginalHandedOver"] = ("выдал(а) оригинал записки на руки", "doc"),
        ["Sz.OriginalReturned"] = ("принял(а) оригинал записки обратно", "doc"),
        ["Sz.RouteChangedByAdmin"] = ("изменил(а) маршрут согласования записки как администратор", "edit"),
        ["Sz.RouteParticipantAdded"] = ("добавил(а) согласующего в маршрут записки", "edit"),
        ["Sz.RouteParticipantRemoved"] = ("исключил(а) согласующего из маршрута записки", "edit"),
        ["Sz.CorrectedOnApproval"] = ("внёс(ла) правки в записку на согласовании", "edit"),
        ["Sz.ApprovalRestartedAfterCorrection"] = ("перезапустил(а) согласование после правки записки", "clock"),
        ["SzAssignment.Reported"] = ("отчитался(ась) по поручению из записки", "check"),
        ["SzAssignment.Accepted"] = ("принял(а) исполнение поручения из записки", "check"),
        ["SzAssignment.Returned"] = ("вернул(а) поручение из записки на доработку", "x"),
        ["SzAssignment.Cancelled"] = ("снял(а) поручение из записки", "x"),

        // ── Закупка ──────────────────────────────────────────────────────────
        ["ProcurementRequest.Created"] = ("создал(а) заявку на закупку", "doc"),
        ["ProcurementRequest.Updated"] = ("изменил(а) заявку на закупку", "edit"),
        ["ProcurementRequest.Deleted"] = ("удалил(а) черновик заявки на закупку", "x"),
        ["ProcurementRequest.SubmittedForApproval"] = ("отправил(а) заявку на закупку на согласование", "clock"),
        ["ProcurementRequest.StatusFromRoute"] = ("изменил(а) статус заявки на закупку по итогу маршрута", "check"),
        ["ProcurementRequest.Withdrawn"] = ("отозвал(а) заявку на закупку", "x"),
        ["ProcurementRequest.Completed"] = ("завершил(а) закупку по заявке", "check"),
        ["ProcurementRequest.WinnerDeclared"] = ("определил(а) победителя закупки", "check"),
        ["ProcurementRequest.ProposalRegistered"] = ("зарегистрировал(а) предложение по закупке", "doc"),
        ["ProcurementRequest.ProposalRemoved"] = ("убрал(а) предложение по закупке", "x"),
        ["ProcurementRequest.ProposalSourcesChanged"] = ("изменил(а) источники предложений по закупке", "edit"),
        ["ProcurementRequest.ProposalVerdict"] = ("вынес(ла) вердикт по предложению в закупке", "check"),
        ["ProcurementRequest.SpecificationSet"] = ("приложил(а) техническую спецификацию к заявке", "doc"),
        ["ProcurementRequest.CompletedWithoutContract"] = ("завершил(а) закупку без договора", "check"),
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
        ["AcknowledgementSheet.ParticipantsAdded"] = ("добавил(а) ознакомляемых в лист ознакомления", "edit"),
        ["AcknowledgementSheet.Closed"] = ("закрыл(а) лист ознакомления", "check"),
        ["AcknowledgementEntry.Acknowledged"] = ("ознакомился(ась) с документом", "check"),
        ["AcknowledgementEntry.Refused"] = ("отказался(ась) от ознакомления с документом", "x"),
        ["AcknowledgementEntry.Cancelled"] = ("снят(а) с ознакомления с документом", "x"),

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
        // ── Заявка на замещение (КСЗ-В9) ─────────────────────────────────────
        ["SubstitutionRequest.Created"] = ("создал(а) заявку на замещение", "doc"),
        ["SubstitutionRequest.Updated"] = ("изменил(а) заявку на замещение", "edit"),
        ["SubstitutionRequest.UpdatedByHr"] = ("внёс(ла) правки УЧР в заявку на замещение", "edit"),
        ["SubstitutionRequest.Submitted"] = ("отправил(а) заявку на замещение на согласование", "clock"),
        ["SubstitutionRequest.Approved"] = ("согласовал(а) заявку на замещение", "check"),
        ["SubstitutionRequest.Rejected"] = ("отклонил(а) заявку на замещение", "x"),
        ["SubstitutionRequest.StepReassigned"] = ("переназначил(а) этап согласования заявки на замещение", "edit"),
        ["SubstitutionRequest.Executed"] = ("отметил(а) исполненной (приказ оформлен) заявку на замещение", "check"),
        ["SubstitutionRequest.Withdrawn"] = ("отозвал(а) в черновик заявку на замещение", "x"),
        ["SubstitutionRequest.Deleted"] = ("удалил(а) черновик заявки на замещение", "x"),
        // Этап «На подписании»: приказ и договор, подписи ЭЦП. Раньше этих действий в словаре не
        // было, и лента показывала их как есть: «Жаныл Эсенова SignedTest», «… SigningStarted»
        // с нейтральным значком.
        ["SubstitutionRequest.SigningStarted"] = ("передал(а) на подписание документы заявки на замещение", "clock"),
        ["SubstitutionRequest.DocumentsRefrozen"] = ("обновил(а) приказ и договор (нужна новая подпись) по заявке на замещение", "edit"),
        ["SubstitutionRequest.Signed"] = ("подписал(а) ЭЦП документ по заявке на замещение", "check"),
        ["SubstitutionRequest.SignedTest"] = ("подписал(а) тестовой имитацией ЭЦП документ по заявке на замещение", "check"),

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
