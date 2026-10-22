namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Результат проверки подлинности согласования по файлу редакции
/// (POST /api/vnd/authenticity/check): что это за файл (SHA-256) и какие согласования/подписи
/// ЭЦП относятся именно к этому тексту.</summary>
public class VndAuthenticityCheckResponse
{
    public required string FileName { get; set; }
    public long FileSize { get; set; }

    /// <summary>SHA-256 загруженного файла, hex upper-case — в том же виде, в каком он входит в
    /// отпечаток текста ("ru=HEX;kg=…;en=…") каждой подписи.</summary>
    public required string FileSha256 { get; set; }

    /// <summary>Совпадения были, но часть документов скрыта правилами видимости (чужой черновик).</summary>
    public bool HasHiddenMatches { get; set; }

    /// <summary>Совпадений больше, чем показано (см. VndAuthenticityService.MaxRedactions).</summary>
    public bool Truncated { get; set; }

    public List<VndAuthenticityRedactionMatch> Matches { get; set; } = [];
}

/// <summary>Редакция ВНД, к которой относится загруженный файл.</summary>
public class VndAuthenticityRedactionMatch
{
    public int VndId { get; set; }
    public required string VndCode { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    public int RedactionId { get; set; }
    public required string RedactionCode { get; set; }
    public int RedactionNumber { get; set; }

    /// <summary>Языковая версия, которой оказался файл: "ru" / "kg" / "en".</summary>
    public required string Language { get; set; }

    /// <summary>Файл — текущий текст этой редакции (а не промежуточная версия, заменённая при
    /// доработке, и не версия, заменённая уже после согласования).</summary>
    public bool IsCurrentText { get; set; }

    /// <summary>Номера промежуточных версий ("Р1.1", "Р1.2" — SnapshotNumber), которыми этот файл
    /// был в ходе доработки. Пусто — файл не был промежуточной версией.</summary>
    public List<int> SnapshotNumbers { get; set; } = [];

    public List<VndAuthenticityProcess> Processes { get; set; } = [];
}

/// <summary>Процесс согласования редакции и то, кто в нём подписал именно этот текст.</summary>
public class VndAuthenticityProcess
{
    public int ApprovalProcessId { get; set; }

    /// <summary>"primary" / "revision_needed" / "repeated" / "final_hold" / "approved" / "cancelled" / "rejected".</summary>
    public required string Status { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsNoChangesActualization { get; set; }

    /// <summary>Сколько действующих согласующих поставили ЭЦП под этим текстом своим итоговым решением.</summary>
    public int SignedUnderThisText { get; set; }

    /// <summary>Сколько всего действующих согласующих (без убранных главным редактором).</summary>
    public int ApproversTotal { get; set; }

    public List<VndAuthenticityApprover> Approvers { get; set; } = [];

    /// <summary>Все подписи ЭЦП под этим текстом в этом процессе — включая подписи промежуточных
    /// кругов, в хронологическом порядке.</summary>
    public List<VndAuthenticitySignature> Signatures { get; set; } = [];

    /// <summary>История согласования: все решения всех кругов (первичное, повторные согласования,
    /// финальная выдержка) с комментариями и комментарии инициатора к исправлениям — в
    /// хронологическом порядке. У решения — его подпись ЭЦП (под этим текстом или под другой версией).</summary>
    public List<VndAuthenticityHistoryEntry> History { get; set; } = [];
}

/// <summary>Одна запись истории согласования.</summary>
public class VndAuthenticityHistoryEntry
{
    /// <summary>"decision" — решение согласующего; "initiator_comment" — комментарий инициатора к
    /// исправлениям, с которым начался круг повторного согласования.</summary>
    public required string Kind { get; set; }

    /// <summary>"primary" / "repeat" / "finalHold".</summary>
    public required string Phase { get; set; }

    public int RoundNumber { get; set; }
    public DateTime? At { get; set; }

    public string? StageTitle { get; set; }
    public string? FullName { get; set; }
    public string? Position { get; set; }

    /// <summary>Как у <see cref="VndAuthenticityApprover.Decision"/>; null — для комментария инициатора.</summary>
    public string? Decision { get; set; }

    public string? Comment { get; set; }

    /// <summary>Как у <see cref="VndAuthenticityApprover.SignatureState"/>; null — для комментария инициатора.</summary>
    public string? SignatureState { get; set; }

    /// <summary>Подпись ЭЦП под этим решением (под этим текстом или под другой версией); null — без ЭЦП.</summary>
    public VndAuthenticitySignature? Signature { get; set; }
}

/// <summary>Согласующий процесса и его итоговое решение — строка, как в Листе согласования.</summary>
public class VndAuthenticityApprover
{
    public string? StageTitle { get; set; }
    public required string FullName { get; set; }
    public string? Position { get; set; }

    /// <summary>"pending" / "approved" / "approved_with_comment" / "rejected" / "auto_approved" / "removed".</summary>
    public required string Decision { get; set; }

    /// <summary>"primary" / "repeat" / "finalHold".</summary>
    public required string Phase { get; set; }

    public DateTime? DecidedAt { get; set; }

    /// <summary>
    /// "signed_this_text" — итоговое решение подписано ЭЦП под этим текстом (см. SignatureId);
    /// "signed_other_text" — подпись под итоговым решением есть, но под другой версией текста;
    /// "auto_initiator" — согласовано автоматически: согласующий — сам инициатор согласования;
    /// "auto" — согласовано автоматически (перенос, истечение срока) без ЭЦП;
    /// "no_signature" — решение без ЭЦП; "pending" — решения ещё нет.
    /// </summary>
    public required string SignatureState { get; set; }

    public int? SignatureId { get; set; }
}

/// <summary>Подпись ЭЦП под этим текстом — запись неизменяемого журнала VndResolutionSignature.
/// ПИН владельца не отдаётся.</summary>
public class VndAuthenticitySignature
{
    public int Id { get; set; }
    public string? StageTitle { get; set; }

    /// <summary>"primary" / "repeat" / "finalHold".</summary>
    public required string Phase { get; set; }
    public int RoundNumber { get; set; }

    /// <summary>"approved" / "approved_with_comment" / "rejected".</summary>
    public required string Decision { get; set; }

    /// <summary>Текст резолюции ровно в том виде, в каком он вошёл в подписанный хэш.</summary>
    public required string CommentText { get; set; }

    public string? AccountFullName { get; set; }
    public string? PersonFio { get; set; }
    public string? OrganizationName { get; set; }
    public string? CertCommonName { get; set; }
    public string? CertIssuerCommonName { get; set; }

    public DateTime SignedAt { get; set; }

    /// <summary>Время из самой CMS-подписи (проставлено провайдером, подделать нельзя).</summary>
    public DateTime? CmsSigningTime { get; set; }

    public bool Verified { get; set; }

    /// <summary>Локальная подпись системы «Делосфера» (сотрудник без ЭЦП КР), а не ЭЦП ГП «Инфоком».</summary>
    public bool IsSystem { get; set; }

    public required string HashSchemaVersion { get; set; }

    /// <summary>Подписанный хэш резолюции (решение + комментарий + отпечаток текста + код ВНД).</summary>
    public required string Hash { get; set; }

    /// <summary>Отпечаток текста ("ru=HEX;kg=…;en=…"), под которым стоит подпись.</summary>
    public required string ContentDigest { get; set; }

    public required string VndCode { get; set; }
    public required string SignatureSha256 { get; set; }

    /// <summary>Подпись стоит под загруженным файлом (его SHA-256 входит в отпечаток текста).</summary>
    public bool UnderThisText { get; set; }
}
