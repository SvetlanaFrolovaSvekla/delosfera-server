using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.Models;

/// <summary>Чем закончилось ознакомление у конкретного человека.</summary>
public enum AcknowledgementState
{
    /// <summary>Ждёт: документ дошёл до сотрудника, он ещё не расписался.</summary>
    Pending = 0,

    /// <summary>Ознакомлен — расписался простой электронной подписью.</summary>
    Acknowledged = 1,

    /// <summary>
    /// Отказался. Отказ — законный исход, а не сбой: сотрудник вправе не согласиться,
    /// и кадровой службе важно, что он документ видел и отказ зафиксирован.
    /// </summary>
    Refused = 2,

    /// <summary>Снят с ознакомления: уволился, переведён, лист отозван.</summary>
    Cancelled = 3,
}

/// <summary>
/// Лист ознакомления с документом.
///
/// Кадровый приказ, положение, регламент вступают в силу не тогда, когда подписаны,
/// а когда о них узнали те, кого они касаются. Спор «я этого не видел» решается
/// только листом ознакомления, поэтому нужен не факт рассылки, а роспись каждого.
///
/// Лист заводится на документ любого вида: кадровым приказом ознакомление не
/// исчерпывается — с внутренними нормативными документами знакомятся так же и чаще.
/// </summary>
public class AcknowledgementSheet
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>Что именно требуется от сотрудника — показывается ему в задаче.</summary>
    public string? Instruction { get; set; }

    /// <summary>
    /// До какого числа ознакомиться. Пусто — срока нет; так бывает у документов,
    /// с которыми знакомятся при поступлении на работу, а не к дате.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Требовать ли подпись. Без подписи ознакомление — отметка в базе, которую
    /// сотрудник может оспорить; с подписью под отметкой стоит его ПЭП со временем
    /// и отпечатком документа.
    /// </summary>
    public bool RequireSignature { get; set; } = true;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Лист закрыт — новых участников не добавляют. Закрытие не отменяет уже
    /// поставленных росписей.
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    public ICollection<AcknowledgementEntry> Entries { get; set; } = new List<AcknowledgementEntry>();
}

/// <summary>
/// Строка листа: один сотрудник и его роспись.
///
/// Хранится как отдельная запись, а не как отметка в списке, потому что у каждой
/// росписи своё время, свой исход и своя подпись — а у отказа ещё и причина.
/// </summary>
public class AcknowledgementEntry
{
    public int Id { get; set; }

    public int SheetId { get; set; }
    public AcknowledgementSheet? Sheet { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// Подразделение на момент включения в лист. Сотрудник переходит из отдела в
    /// отдел, а лист должен остаться таким, каким был: иначе через год непонятно,
    /// почему с приказом по операционному управлению знакомился человек из ИТ.
    /// </summary>
    public int? OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    public AcknowledgementState State { get; set; } = AcknowledgementState.Pending;

    public DateTime? RespondedAt { get; set; }

    /// <summary>Причина отказа — без неё отказ нечего обсуждать.</summary>
    public string? Comment { get; set; }

    /// <summary>Подпись под ознакомлением. Пусто, если лист подписи не требовал.</summary>
    public int? SignatureId { get; set; }

    public DateTime CreatedAt { get; set; }
}
