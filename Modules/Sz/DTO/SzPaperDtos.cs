namespace delosfera_server.Modules.Sz.DTO;

public class SzHandoverRequest
{
    /// <summary>Кому выдан оригинал.</summary>
    public int HolderUserId { get; set; }

    /// <summary>К какой дате оригинал ждут обратно.</summary>
    public DateOnly? DueBackOn { get; set; }

    /// <summary>Где будет находиться оригинал.</summary>
    public string? Location { get; set; }
}

/// <summary>Состояние бумажного оригинала записки.</summary>
public class SzOriginalResponse
{
    public int SzId { get; set; }
    public string? RegNumber { get; set; }
    public string? Title { get; set; }

    public bool IsPaperCarrier { get; set; }

    public int? HolderUserId { get; set; }
    public string? HolderName { get; set; }
    public DateTime? HandedAt { get; set; }
    public DateOnly? DueBackOn { get; set; }
    public string? Location { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public int HandoverCount { get; set; }

    /// <summary>Оригинал на руках: выдан и ещё не возвращён.</summary>
    public bool IsOut { get; set; }

    /// <summary>Возврат просрочен.</summary>
    public bool IsOverdue { get; set; }
    public int? DaysLeft { get; set; }

    /// <summary>
    /// Текущий пользователь вправе выдавать и принимать оригинал. Смотреть состояние
    /// может любой, кто видит записку, — кнопки действий клиент показывает по этому признаку.
    /// </summary>
    public bool CanManage { get; set; }
}

/// <summary>Печатная форма записки с листом согласования (SZ-PAP).</summary>
public class SzPrintFormResponse
{
    public string? RegNumber { get; set; }
    public DateOnly? RegisteredOn { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public string? Kind { get; set; }
    public string? HrKind { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorUnit { get; set; }
    public string? CorrespondentUnit { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsPaperCarrier { get; set; }

    /// <summary>Реквизиты вида: сумма, бюджет, ФИО сотрудника — то, что заполнено.</summary>
    public List<SzPrintFieldResponse> Fields { get; set; } = [];

    /// <summary>Лист согласования: кто, когда и с каким решением визировал.</summary>
    public List<SzPrintApprovalResponse> Approvals { get; set; } = [];

    /// <summary>Резолюция руководителя и поручения — печатаются, если уже вынесены.</summary>
    public string? ExecutionResolution { get; set; }
    public List<SzPrintAssignmentResponse> Assignments { get; set; } = [];

    /// <summary>Решение адресата по существу вопроса — печатается, если вынесено.</summary>
    public string? AddresseeName { get; set; }
    public string? AddresseeDecision { get; set; }
    public DateTime? AddresseeDecisionAt { get; set; }

    /// <summary>
    /// Отпечаток ТЕКУЩЕГО содержимого записки (SHA-256, тот же компонент "content", что входит в хэш
    /// каждой подписи ЭЦП). Лист с подписями показывает его в подвале: по нему видно, под какой
    /// версией текста стоит каждая подпись.
    /// </summary>
    public string? ContentDigest { get; set; }
}

public class SzPrintFieldResponse
{
    public required string Label { get; set; }
    public required string Value { get; set; }
}

public class SzPrintApprovalResponse
{
    public int StepOrder { get; set; }

    /// <summary>Вид этапа: согласование или подписание — в листе они различаются.</summary>
    public string? StepKind { get; set; }
    public string? UserName { get; set; }
    public string? Position { get; set; }
    public string? Unit { get; set; }

    /// <summary>Решение; пусто — виза ещё не поставлена, в печатной форме останется место для подписи.</summary>
    public string? Resolution { get; set; }
    public string? Comment { get; set; }
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Штамп электронной подписи под визой. Пусто — визы нет, и в печатной форме
    /// на этом месте остаётся линейка под подпись от руки.
    /// </summary>
    public SzPrintSignatureResponse? Signature { get; set; }
}

/// <summary>Реквизиты штампа для печати (SIG-03).</summary>
public class SzPrintSignatureResponse
{
    public required string LevelTitle { get; set; }
    public string? FullName { get; set; }
    public string? Position { get; set; }
    public DateTime At { get; set; }
    public string? Fingerprint { get; set; }
    public bool Revoked { get; set; }
    public string? RevokedReason { get; set; }

    // --- ЭЦП ГП «Инфоком» (журнал SzResolutionSignature) — заполнено, только если IsEcp ---

    /// <summary>Подпись поставлена ЭЦП ГП «Инфоком» и проверена у провайдера при сохранении резолюции.</summary>
    public bool IsEcp { get; set; }

    /// <summary>Тестовая имитация ЭЦП (EcpSigning:AllowSzTestEcp) — не подпись; печатается с пометкой «ТЕСТ».</summary>
    public bool IsTest { get; set; }

    /// <summary>Организация из ответа провайдера (если подписывали от её имени).</summary>
    public string? OrganizationName { get; set; }

    /// <summary>Срок действия сертификата — как его вернул провайдер.</summary>
    public string? CertValidNotAfter { get; set; }

    /// <summary>Издатель сертификата.</summary>
    public string? IssuerName { get; set; }

    /// <summary>Подписанный хэш резолюции (SHA-256, hex).</summary>
    public string? DocumentHash { get; set; }

    /// <summary>SHA-256 самой CMS-подписи (hex) — отпечаток подписи.</summary>
    public string? SignatureSha256 { get; set; }

    /// <summary>Подпись стоит под текущей версией содержимого записки. false — записку меняли после
    /// подписания (например, на прошлом круге согласования); null — сверка неприменима.</summary>
    public bool? MatchesCurrentContent { get; set; }
}

public class SzPrintAssignmentResponse
{
    public string? AssigneeName { get; set; }
    public required string Text { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsPrimary { get; set; }
}
