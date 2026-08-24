using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Correspondence.Models;

/// <summary>
/// Кто пишет и кому пишут. Вид определяет, какие сроки и правила включаются:
/// у регулятора свои, у клиента — установленные законом.
/// </summary>
public enum CorrespondentKind
{
    /// <summary>Национальный банк — предписания и запросы с обязательным сроком.</summary>
    Regulator = 1,

    /// <summary>Иной государственный орган: суд, налоговая, ГСФР, следственные органы.</summary>
    Government = 2,

    /// <summary>Банк или иная финансовая организация.</summary>
    Bank = 3,

    /// <summary>Клиент — юридическое лицо.</summary>
    ClientCompany = 4,

    /// <summary>Клиент — физическое лицо.</summary>
    ClientPerson = 5,

    /// <summary>Поставщик, подрядчик, контрагент по договору.</summary>
    Counterparty = 6,

    Other = 9,
}

/// <summary>
/// Корреспондент: организация или человек, с которым банк переписывается.
///
/// Заводится отдельно от письма, а не строкой в нём, ради одного вопроса: «что у
/// нас с этим корреспондентом за последний год». Пока наименование живёт текстом
/// внутри письма, «Национальный банк КР», «НБ КР» и «Нацбанк» — три разных
/// адресата, и переписка по одному делу не собирается.
/// </summary>
public class Correspondent : IAuditableEntity
{
    public int Id { get; set; }

    public required string Title { get; set; }

    /// <summary>Краткое имя для списков: «НБКР» вместо полного наименования.</summary>
    public string? ShortTitle { get; set; }

    public CorrespondentKind Kind { get; set; } = CorrespondentKind.Other;

    /// <summary>ИНН или ОКПО — для юридических лиц.</summary>
    public string? TaxId { get; set; }

    public string? Address { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>Контактное лицо со стороны корреспондента.</summary>
    public string? ContactPerson { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
