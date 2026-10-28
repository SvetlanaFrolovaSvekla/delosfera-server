using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Substitutions.Models;

/// <summary>Какой документ заявки на замещение подписывается.</summary>
public enum SubstitutionDocumentKind
{
    /// <summary>Приказ «О временном возложении обязанностей» (форма 41-29-01/ЛС).</summary>
    Order = 1,

    /// <summary>Договор о полной индивидуальной материальной ответственности (форма 41-15-01/МО).</summary>
    Liability = 2,
}

/// <summary>В каком качестве ставится подпись под документом заявки.</summary>
public enum SubstitutionSignerRole
{
    /// <summary>Директор: подписывает приказ и договор от имени Банка.</summary>
    Director = 1,

    /// <summary>Отсутствующий сотрудник: «ознакомлен и согласен» под приказом.</summary>
    Absent = 2,

    /// <summary>Замещающий: «ознакомлен и согласен» под приказом и «Работник» в договоре.</summary>
    Substitute = 3,

    /// <summary>
    /// Дополнительный подписант договора от имени Банка (кроме основного — Председателя Правления
    /// из настроек маршрута УЧР). Добавляется делопроизводителем в заявке; таких мест может быть
    /// несколько — место определяется ролью и сотрудником.
    /// </summary>
    BankSigner = 4,
}

/// <summary>
/// Зафиксированная версия печатного документа заявки — то, что подписывают на этапе
/// «На подписании». Раньше приказ и договор собирались заново при каждом скачивании, и
/// подписывать было нечего: следующая выгрузка могла отличаться от той, что видел подписант.
/// Теперь файл сохраняется один раз при переходе заявки к подписанию; правка УЧР на этом этапе
/// заводит новую версию (IsCurrent переходит к ней), подписи старой версии остаются в журнале.
/// </summary>
public class SubstitutionDocument
{
    public int Id { get; set; }

    public int RequestId { get; set; }
    public SubstitutionRequest? Request { get; set; }

    public SubstitutionDocumentKind Kind { get; set; }

    /// <summary>Версия документа внутри заявки: 1, 2, … — растёт при каждой правке УЧР на подписании.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Действующая версия — её подписывают и скачивают.</summary>
    public bool IsCurrent { get; set; } = true;

    public required string FileName { get; set; }

    /// <summary>Файл .docx как есть (несколько десятков КБ).</summary>
    public required byte[] Content { get; set; }

    /// <summary>SHA-256 файла (hex upper-case) — входит в подписываемый хэш.</summary>
    public required string ContentSha256 { get; set; }

    public DateTime CreatedAt { get; set; }

    // Подписанты фиксируются вместе с версией документа: кто должен подписать именно этот файл.
    // null — подписанта нет (у ГО не задан подписант в настройках, отсутствующий/замещающий не
    // выбран из сотрудников) — место подписи показывается пустым и полноту подписания не держит.

    /// <summary>Директор: подписывает приказ от имени Банка. У договора — основной подписант от Банка
    /// (Председатель Правления из настроек маршрута УЧР).</summary>
    public int? DirectorUserId { get; set; }

    /// <summary>Отсутствующий сотрудник (только приказ: «ознакомлен и согласен»).</summary>
    public int? AbsentUserId { get; set; }

    /// <summary>Замещающий (приказ — «ознакомлен и согласен», договор — «Работник»).</summary>
    public int? SubstituteUserId { get; set; }

    /// <summary>
    /// Дополнительные подписанты договора от имени Банка (кроме основного в DirectorUserId) —
    /// зафиксированы с версией документа. У приказа пусто.
    /// </summary>
    public int[] ExtraBankSignerUserIds { get; set; } = [];
}

/// <summary>
/// Подпись ЭЦП ГП «Инфоком» под документом заявки на замещение — как журналы подписей ВНД и СЗ:
/// доказательная запись «кто, чем, что именно и когда подписал». НЕИЗМЕНЯЕМАЯ: строку можно
/// только добавить (DelosferaDbContext.ApplyAuditInfo и триггер миграции AddSubstitutionSigning).
/// Токен Инфокома и пин-код сюда не пишутся.
/// </summary>
public class SubstitutionSignature
{
    public int Id { get; set; }

    public int RequestId { get; set; }
    public SubstitutionRequest? Request { get; set; }

    /// <summary>Подписанная версия документа.</summary>
    public int DocumentId { get; set; }
    public SubstitutionDocument? Document { get; set; }

    public SubstitutionSignerRole Role { get; set; }

    /// <summary>Пользователь delosfera, который поставил подпись.</summary>
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>За кого подписано (директор в отпуске — подписывает его замещающий, GEN-14).</summary>
    public int? OnBehalfOfUserId { get; set; }

    /// <summary>ПИН владельца подписи ИЗ ОТВЕТА 2.6 check-sign.</summary>
    public required string PersonIdnp { get; set; }
    public string? PersonFio { get; set; }
    public string? OrganizationInn { get; set; }
    public string? OrganizationName { get; set; }

    public string? CertKeyIdentifier { get; set; }
    public string? CertCommonName { get; set; }
    public string? CertValidNotBefore { get; set; }
    public string? CertValidNotAfter { get; set; }
    public string? CertIssuerCommonName { get; set; }
    public string? CertIssuerOrganizationName { get; set; }

    public required string HashSchemaVersion { get; set; }

    /// <summary>SHA-256 канонической строки (hex upper-case) — ровно то, что уходило в 2.5.</summary>
    public required string Hash { get; set; }

    /// <summary>CMS/PKCS#7-подпись целиком (base64).</summary>
    public required string SignatureBase64 { get; set; }

    /// <summary>SHA-256 байтов подписи — уникальный индекс, защита от повторного предъявления.</summary>
    public required string SignatureSha256 { get; set; }

    public DateTime SignedAt { get; set; }
    public DateTime? ProviderTimestamp { get; set; }
    public DateTime? CmsSigningTime { get; set; }
    public DateTime VerifiedAt { get; set; }
    public DateTime? ProviderCheckTimestamp { get; set; }

    /// <summary>false — тестовая имитация ЭЦП (EcpSigning:AllowSzTestEcp), не подпись.</summary>
    public bool Verified { get; set; }
    public string? VerificationError { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>
    /// Место подписи в ЭЦП-версии документа, выбранное подписантом в окне подписания: после какого
    /// абзаца текста (индекс в документе без рукописного блока подписей) стоит блок подписи.
    /// null — место не выбиралось (подпись до появления выбора): блок ставится в конце текста.
    /// В подписываемый хэш не входит — подписывается сам зафиксированный файл, место — оформление.
    /// </summary>
    public int? PlacementAnchor { get; set; }

    /// <summary>Отступ блока подписи от левого поля, мм (0…85).</summary>
    public int? PlacementIndentMm { get; set; }
}
