using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Meetings.Models;

/// <summary>Кем человек входит в коллегиальный орган.</summary>
public enum BodyRole
{
    /// <summary>Член органа: голосует и видит повестку целиком.</summary>
    Member = 0,

    /// <summary>Председатель — он один, и он выносит вопросы на орган.</summary>
    Chairman = 1,

    /// <summary>Секретарь органа: ведёт повестку и протокол.</summary>
    Secretary = 2,
}

/// <summary>
/// Состав коллегиального органа: кто входит в Правление, КПА, Кредитный комитет.
///
/// Раньше состав задавался правами роли, и это дважды подвело. Роли, заведённые
/// «всеми правами», делали членами Правления администраторов и редакторов ВНД. А
/// председателя пытались узнать по праву «выносить вопрос на орган» — но такое
/// право по работе есть и у администратора системы, и он оказывался первым в
/// списке «Кому».
///
/// Состав органа — не набор прав, а список людей: банк меняет его решением, а не
/// перенастройкой доступа. Право отвечает на вопрос «что человеку можно», состав
/// — на вопрос «кто он в этом органе».
/// </summary>
public class BodyMember
{
    public int Id { get; set; }

    public MeetingBody Body { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public BodyRole Role { get; set; } = BodyRole.Member;

    /// <summary>
    /// С какого дня человек в составе. Пусто — состав ведётся без дат, как чаще
    /// всего и бывает: список правят по факту решения.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>По какой день. Пусто — состоит и сейчас.</summary>
    public DateOnly? To { get; set; }

    /// <summary>Основание: протокол, приказ, решение собрания.</summary>
    public string? Basis { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Явка члена органа на заседание.
///
/// Кворум считается по присутствовавшим, и протокол начинается со списка: кто
/// был, кто отсутствовал и почему. Отметка заводится только для тех, кто не
/// пришёл: по умолчанию член органа на заседании присутствует, и отмечать
/// каждого пришедшего значило бы заставлять секретаря щёлкать по всему составу.
/// </summary>
public class MeetingAttendance
{
    public int Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public int UserId { get; set; }
    public delosfera_server.Modules.Users.Models.User? User { get; set; }

    /// <summary>Присутствовал ли. Записи нет — значит присутствовал.</summary>
    public bool Present { get; set; } = true;

    /// <summary>Причина отсутствия: отпуск, командировка, болезнь.</summary>
    public string? Note { get; set; }

    public int MarkedByUserId { get; set; }
    public DateTime MarkedAt { get; set; }
}
