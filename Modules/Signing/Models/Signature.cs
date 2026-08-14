namespace delosfera_server.Modules.Signing.Models;

/// <summary>Уровень электронной подписи (раздел 7 ТЗ, Закон КР «Об ЭП»).</summary>
public enum SignatureLevel
{
    /// <summary>Простая ЭП (ПЭП) — внутренние визы/резолюции.</summary>
    Simple = 0,

    /// <summary>Квалифицированная ЭП (КЭП/ЭЦП) — юридически значимые документы.</summary>
    Qualified = 1
}

/// <summary>
/// Электронная подпись хеша версии вложения (SIG-01): при изменении файла подпись
/// аннулируется (Revoked) и процесс блокируется.
/// </summary>
public class Signature
{
    public int Id { get; set; }

    /// <summary>
    /// Подписанное вложение (Documents.DocumentAttachment) — фиксирует хеш версии.
    /// Пусто, когда подписана карточка документа: у служебной записки текст живёт
    /// в самой карточке, файла может не быть вовсе, а виза нужна всё равно.
    /// </summary>
    public int? DocumentAttachmentId { get; set; }

    /// <summary>Подписанный документ — заполняется, когда подпись легла на карточку.</summary>
    public int? DocumentId { get; set; }

    /// <summary>
    /// Отпечаток подписанного: для вложения это хеш версии файла, для карточки —
    /// свёртка её существенных полей вместе с хешами вложений. По нему видно,
    /// изменилось ли то, под чем стоит подпись.
    /// </summary>
    public string? ContentHash { get; set; }

    public int UserId { get; set; }

    public SignatureLevel Level { get; set; }

    public DateTime At { get; set; }

    /// <summary>Метаданные штампа (ФИО, должность, тип сертификата) — JSON.</summary>
    public string? StampMeta { get; set; }

    public bool Revoked { get; set; }
    public string? RevokedReason { get; set; }
}
