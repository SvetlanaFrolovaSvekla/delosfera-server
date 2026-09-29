namespace delosfera_server.Modules.Documents.VND.DTO.Request;

public class ApprovalDecisionRequest
{
    public required ApprovalDecisionType Decision { get; set; }

    /// <summary>Обязателен для ApproveWithComment и Reject</summary>
    public string? Comment { get; set; }

    /// <summary>Файлы, приложенные согласующим к своей резолюции (необязательно).
    /// Хранятся бессрочно, наравне с текстом комментария, — остаются частью истории
    /// согласования и после того, как редакция станет согласованной.</summary>
    public List<IFormFile>? Files { get; set; }

    /// <summary>Цитаты из текста редакции, на которые согласующий сослался в Comment (см.
    /// "+ Сослаться на текст редакции" на клиенте) — JSON-массив ApprovalQuoteItem. Передаётся
    /// одним form-полем (а не как список сложных объектов через [FromForm]) по тем же причинам,
    /// по которым Files выше забирается вручную из Request.Form.Files — комплексный биндинг
    /// списков через [FromForm] в ASP.NET Core ненадёжен.</summary>
    public string? QuotesJson { get; set; }

    // --- ЭЦП ГП «Инфоком» (SIG-ECP) --------------------------------------------------------
    //
    // Для Decision == Approve подпись ОБЯЗАТЕЛЬНА (без неё — 400). Согласно документации
    // провайдера ЭП нельзя использовать как первый фактор входа, поэтому она прикладывается к самой
    // резолюции: фронт запрашивает хэш у backend (POST .../decision/resolution-hash — хэш считается
    // ТОЛЬКО на сервере из комментария + содержимого текущей редакции + кода ВНД, см.
    // ResolutionSignatureHasher), подписывает его в 2.5 и присылает сюда. VndApprovalService.
    // DecideAsync → ResolutionSignatureService.VerifyForDecisionAsync пересчитывает хэш из БД,
    // проверяет подпись в 2.6 по ПЕРЕСЧИТАННОМУ хэшу, личность подписанта и свежесть подписи, и
    // только потом сохраняет решение вместе с записью журнала VndResolutionSignature.
    // Для остальных решений эти поля игнорируются.

    /// <summary>base64 CMS/PKCS7-подпись хэша резолюции, полученная от ГП (2.5 get-sign/for-hash).</summary>
    public string? EcpSignatureBase64 { get; set; }

    /// <summary>Токен доступа ГП (2.3 account/auth). Используется ТОЛЬКО "на лету" для 2.4
    /// get-cert-info (сведения о сертификате в журнал) и нигде не сохраняется.</summary>
    public string? EcpCertToken { get; set; }

    /// <summary>Хэш, который был подписан (hex) — тот, что вернул resolution-hash.</summary>
    public string? EcpHash { get; set; }

    /// <summary>ПИН физ. лица, введённый при получении токена (2.3) — 14 цифр. Сверяется с ПИН
    /// владельца подписи из ответа 2.6.</summary>
    public string? EcpPersonIdnp { get; set; }

    /// <summary>timestamp из ответа 2.5 (Unix time, мс). Запасной источник времени подписи,
    /// если в самой CMS нет signingTime.</summary>
    public long? EcpTimestamp { get; set; }

    /// <summary>ТЕСТОВЫЙ режим: решение без ЭЦП (любое — "Согласовать", "с замечаниями", "Отклонить").
    /// Разрешено, только если на сервере включено EcpSigning:AllowWithoutEcp (иначе 403, проверяет
    /// VndApprovalController). При true подпись не проверяется и запись в журнал VndResolutionSignature
    /// не создаётся.</summary>
    public bool WithoutEcp { get; set; }

    /// <summary>IP клиента — заполняет контроллер из соединения, из формы НЕ биндится.</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? RequestIpAddress { get; set; }

    /// <summary>User-Agent клиента — заполняет контроллер из заголовка, из формы НЕ биндится.</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? RequestUserAgent { get; set; }
}

/// <summary>Один элемент QuotesJson — см. VndApprovalStageQuote на бэке.</summary>
public class ApprovalQuoteItem
{
    /// <summary>"ru"/"kg"/"en"/"tid"/"approvalSheet"/"disagreementMatrix" — см.
    /// RedactionViewTarget на клиенте.</summary>
    public required string DocumentTarget { get; set; }

    public required string Text { get; set; }

    /// <summary>Контекст перед/после цитаты и номер вхождения - "якорь" для точного поиска места
    /// в тексте, см. VndApprovalStageQuote.Prefix/Suffix/Occurrence. Необязательны.</summary>
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public int? Occurrence { get; set; }

    /// <summary>Замечание согласующего к этому фрагменту - см. VndApprovalStageQuote.Note.</summary>
    public string? Note { get; set; }
}

public enum ApprovalDecisionType
{
    Approve = 0,
    ApproveWithComment = 1,
    Reject = 2
}

public static class ApprovalDecisionTypeExtensions
{
    public static delosfera_server.Modules.Documents.VND.Models.ApprovalStageDecision ToStageDecision(
        this ApprovalDecisionType type) => type switch
    {
        ApprovalDecisionType.Approve => delosfera_server.Modules.Documents.VND.Models.ApprovalStageDecision.Approved,
        ApprovalDecisionType.ApproveWithComment => delosfera_server.Modules.Documents.VND.Models.ApprovalStageDecision.ApprovedWithComment,
        ApprovalDecisionType.Reject => delosfera_server.Modules.Documents.VND.Models.ApprovalStageDecision.Rejected,
        _ => throw new InvalidOperationException("Неизвестный тип решения"),
    };
}