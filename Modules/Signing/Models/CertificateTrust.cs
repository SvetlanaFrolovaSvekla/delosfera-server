namespace delosfera_server.Modules.Signing.Models;

/// <summary>
/// Корневой или промежуточный сертификат удостоверяющего центра, которому банк доверяет.
///
/// Без этого списка проверка подписи отвечает только на вопрос «подпись соответствует
/// сертификату», но не на вопрос «а чей это сертификат». Самоподписанный сертификат,
/// выпущенный кем угодно на чьё угодно имя, проходил бы наравне с выданным УЦ.
///
/// Список ведёт администратор: состав удостоверяющих центров — решение банка, а не
/// свойство программы, и меняется он отзывом доверия, а не пересборкой.
/// </summary>
public class TrustedCertificateAuthority
{
    public int Id { get; set; }

    /// <summary>Как называть центр в интерфейсе: «УЦ Инфоком», «Тестовый УЦ стенда».</summary>
    public required string Title { get; set; }

    /// <summary>Кому выдан — из самого сертификата, для показа администратору.</summary>
    public required string Subject { get; set; }

    /// <summary>Кем выдан. У корневого совпадает с Subject.</summary>
    public required string Issuer { get; set; }

    /// <summary>Отпечаток SHA-1 — по нему сертификат отличают от похожего.</summary>
    public required string Thumbprint { get; set; }

    public required string SerialNumber { get; set; }

    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }

    /// <summary>Сам сертификат в DER — из него строится цепочка при проверке.</summary>
    public required byte[] RawData { get; set; }

    /// <summary>
    /// Доверие снимают, а не удаляют: подписи, проверенные по этому центру раньше,
    /// должны остаться объяснимыми.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public int AddedByUserId { get; set; }
    public DateTime AddedAt { get; set; }

    public string? DisabledReason { get; set; }
    public DateTime? DisabledAt { get; set; }
}

/// <summary>
/// Сертификат, которым подписывает конкретный сотрудник.
///
/// Проверка «подпись сходится с сертификатом» не отвечает на вопрос, чей это
/// сертификат: подписать чужой визой можно было бы любым действующим ключом.
/// Поэтому сертификат закрепляется за человеком — при первой подписи, если он
/// свободен, и дальше принимается только от него.
///
/// Отпечаток хранится отдельным полем, потому что именно по нему сертификат
/// опознаётся: серийный номер уникален только внутри одного удостоверяющего центра.
/// </summary>
public class UserCertificate
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public required string Thumbprint { get; set; }

    public required string Subject { get; set; }
    public required string Issuer { get; set; }
    public required string SerialNumber { get; set; }

    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }

    /// <summary>Сертификат целиком — чтобы показать реквизиты, не спрашивая рабочее место.</summary>
    public required byte[] RawData { get; set; }

    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Когда сертификат отозван у сотрудника — увольнение, компрометация ключа,
    /// замена по сроку. Отозванным подписывать нельзя, но прежние подписи остаются.
    /// </summary>
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
}
