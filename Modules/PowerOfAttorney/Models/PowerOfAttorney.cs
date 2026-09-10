using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.PowerOfAttorney.Models;

/// <summary>
/// Состояние доверенности. Рассчитывается не по датам, а хранится: доверенность
/// может кончиться раньше срока — её отзывают, а сотрудник увольняется. Дата
/// окончания говорит, когда доверенность перестанет действовать сама; состояние —
/// действует ли она сейчас.
/// </summary>
public enum PoaStatus
{
    /// <summary>Проект: реквизиты заполнены, на подпись не отдана.</summary>
    Draft = 0,

    /// <summary>На согласовании и подписании.</summary>
    OnApproval = 1,

    /// <summary>Подписана и действует.</summary>
    Active = 2,

    /// <summary>Отозвана до истечения срока.</summary>
    Revoked = 3,

    /// <summary>Срок истёк.</summary>
    Expired = 4,
}

/// <summary>
/// Кому выдана доверенность. Чаще всего сотруднику банка, но не всегда: доверенность
/// выдают и стороннему представителю — адвокату, оценщику, экспедитору.
/// </summary>
public enum PoaHolderKind
{
    /// <summary>Сотрудник банка — есть учётная запись.</summary>
    Employee = 1,

    /// <summary>Стороннее лицо — только ФИО и документ.</summary>
    External = 2,
}

/// <summary>
/// Доверенность: кто, кому, на что и на какой срок.
///
/// Учёт нужен не ради полноты картотеки. Полномочие подписывать от имени банка —
/// это то, чем банк отвечает перед третьими лицами: доверенность, о которой забыли
/// и не отозвали, продолжает действовать, даже когда сотрудник давно перешёл в
/// другое подразделение. Реестр отвечает на два вопроса — «вправе ли этот человек
/// подписать вот это сегодня» и «что мы выдали и не забрали обратно».
/// </summary>
public class PowerOfAttorney : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>
    /// Регистрационный номер в книге доверенностей. Нумерация сквозная в пределах
    /// года — как во всех книгах регистрации банка.
    /// </summary>
    public string? RegNumber { get; set; }

    public int Year { get; set; }

    /// <summary>Дата выдачи — с неё доверенность и отсчитывается.</summary>
    public DateOnly IssuedOn { get; set; }

    // --- от кого ---

    /// <summary>
    /// Доверитель — должностное лицо, подписывающее доверенность от имени банка.
    /// Обычно Председатель Правления или его заместитель по доверенности.
    /// </summary>
    public int GrantorUserId { get; set; }
    public User? GrantorUser { get; set; }

    /// <summary>
    /// Основание полномочий доверителя: устав, либо доверенность, по которой он сам
    /// действует. Для передоверия здесь стоит родительская доверенность.
    /// </summary>
    public int? ParentPoaId { get; set; }
    public PowerOfAttorney? ParentPoa { get; set; }

    // --- кому ---

    public PoaHolderKind HolderKind { get; set; } = PoaHolderKind.Employee;

    /// <summary>Сотрудник-представитель. Пусто, если доверенность выдана стороннему лицу.</summary>
    public int? HolderUserId { get; set; }
    public User? HolderUser { get; set; }

    /// <summary>ФИО представителя. У сотрудника заполняется тоже — на момент выдачи фамилия могла быть другой.</summary>
    public required string HolderName { get; set; }

    /// <summary>Должность представителя на дату выдачи.</summary>
    public string? HolderPosition { get; set; }

    public int? HolderUnitId { get; set; }
    public OrganizationUnit? HolderUnit { get; set; }

    /// <summary>Документ, удостоверяющий личность: вид, серия, номер, кем и когда выдан.</summary>
    public string? HolderIdentityDocument { get; set; }

    // --- на что ---

    /// <summary>
    /// Полномочия по доверенности — текстом, как они записаны в самом документе.
    /// Хранится дословно: спор о том, входило ли действие в полномочия, решается
    /// по формулировке, а не по нашей рубрике.
    /// </summary>
    public required string Powers { get; set; }

    /// <summary>
    /// Поисковый вектор по ФИО представителя и тексту полномочий. Вычисляемая
    /// колонка — искать доверенность приходится по фразе из полномочий.
    /// </summary>
    public NpgsqlTypes.NpgsqlTsVector? SearchVector { get; set; }

    /// <summary>Право передоверия.</summary>
    public bool AllowsDelegation { get; set; }

    /// <summary>Предельная сумма сделки, если полномочие ограничено суммой.</summary>
    public decimal? AmountLimit { get; set; }
    public string? AmountCurrency { get; set; }

    // --- срок ---

    /// <summary>Действует с. Обычно совпадает с датой выдачи, но не обязана.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>
    /// Действует по включительно. Не может быть пустым: бессрочных доверенностей не
    /// бывает, а доверенность без срока в реестре — это доверенность, о которой
    /// забудут.
    /// </summary>
    public DateOnly ValidTo { get; set; }

    // --- состояние ---

    public PoaStatus Status { get; set; } = PoaStatus.Draft;

    public DateTime? SignedAt { get; set; }
    public int? SignedByUserId { get; set; }

    /// <summary>Когда и почему отозвана. Причина обязательна — отзыв касается третьих лиц.</summary>
    public DateOnly? RevokedOn { get; set; }
    public string? RevokeReason { get; set; }
    public int? RevokedByUserId { get; set; }
    public User? RevokedByUser { get; set; }

    // --- бумажный оригинал ---

    /// <summary>
    /// Где лежит подписанный оригинал. Доверенность предъявляют на бумаге, и вопрос
    /// «у кого она сейчас» возникает чаще, чем кажется.
    /// </summary>
    public string? OriginalLocation { get; set; }

    // TODO: учёт бумажного оригинала доверенности пока не реализован — ни один эндпоинт
    // эти поля не пишет, они лишь отдаются в DTO (всегда null). Либо завести операции
    // выдачи/возврата оригинала (как у СЗ в SzPaperService), либо убрать поля и колонки.
    /// <summary>Оригинал выдан на руки представителю.</summary>
    public DateTime? OriginalHandedAt { get; set; }

    /// <summary>Оригинал возвращён — при отзыве его положено забрать.</summary>
    public DateTime? OriginalReturnedAt { get; set; }

    /// <summary>Скан подписанной доверенности и приложения.</summary>
    public ICollection<PoaFile> Files { get; set; } = new List<PoaFile>();

    /// <summary>Передоверия, выданные по этой доверенности.</summary>
    public ICollection<PowerOfAttorney> Children { get; set; } = new List<PowerOfAttorney>();

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Действует ли доверенность на указанный день. Не свойство, а метод с датой:
    /// вопрос всегда звучит «действовала ли она тогда», а не «действует ли сейчас» —
    /// проверять полномочие приходится и задним числом, при разборе подписанного.
    /// </summary>
    public bool IsValidOn(DateOnly day) =>
        Status == PoaStatus.Active
        && day >= ValidFrom
        && day <= ValidTo
        && (RevokedOn is null || day < RevokedOn.Value);
}

/// <summary>Файл доверенности: скан подписанного оригинала, приложение, отзыв.</summary>
public class PoaFile
{
    public int Id { get; set; }

    public int PowerOfAttorneyId { get; set; }
    public PowerOfAttorney? PowerOfAttorney { get; set; }

    public int FileId { get; set; }
    public Files.Models.FileAttachment? File { get; set; }

    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
