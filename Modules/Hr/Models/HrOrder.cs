using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Hr.Models;

/// <summary>
/// Вид кадрового приказа. Совпадает с видами кадровых записок не случайно: записка
/// — это просьба, приказ — решение по ней, и вид у них один и тот же.
/// </summary>
public enum HrOrderKind
{
    /// <summary>Приём на работу.</summary>
    Hiring = 1,

    /// <summary>Перевод на другую должность или в другое подразделение.</summary>
    Transfer = 2,

    /// <summary>Увольнение.</summary>
    Dismissal = 3,

    /// <summary>Отпуск: ежегодный, без содержания, учебный.</summary>
    Leave = 4,

    /// <summary>Командировка.</summary>
    BusinessTrip = 5,

    /// <summary>Изменение оклада или надбавки.</summary>
    Salary = 6,

    /// <summary>Премирование.</summary>
    Bonus = 7,

    /// <summary>Дисциплинарное взыскание.</summary>
    Discipline = 8,

    /// <summary>Обучение и повышение квалификации.</summary>
    Training = 9,

    /// <summary>Совмещение, замещение, исполнение обязанностей.</summary>
    Combination = 10,

    Other = 99,
}

/// <summary>Состояние приказа.</summary>
public enum HrOrderStatus
{
    /// <summary>Проект — номера ещё нет.</summary>
    Draft = 0,

    /// <summary>На подписании у руководителя.</summary>
    OnSigning = 1,

    /// <summary>Подписан и зарегистрирован в книге.</summary>
    Signed = 2,

    /// <summary>Отменён другим приказом.</summary>
    Cancelled = 3,
}

/// <summary>
/// Приказ по личному составу.
///
/// Кадровые записки в системе уже есть — но записка это просьба, а не решение.
/// Между «прошу направить в командировку» и фактом командировки стоит приказ: он
/// подписывается, регистрируется в отдельной книге и с ним знакомят сотрудника под
/// роспись. Без него контур кадров обрывается на полпути: просьбы есть, решений нет.
///
/// Книга приказов по личному составу ведётся отдельно от прочих: срок хранения у
/// неё особый, и смешивать её с приказами по основной деятельности нельзя.
/// </summary>
public class HrOrder : IAuditableEntity
{
    public int Id { get; set; }

    public HrOrderKind Kind { get; set; } = HrOrderKind.Other;
    public HrOrderStatus Status { get; set; } = HrOrderStatus.Draft;

    /// <summary>Номер в книге приказов по личному составу. Нумерация своя, в пределах года.</summary>
    public string? RegNumber { get; set; }

    public int Year { get; set; }

    /// <summary>Дата приказа — та, что стоит в самом документе.</summary>
    public DateOnly? OrderDate { get; set; }

    /// <summary>
    /// Дата, с которой приказ действует. У командировки и отпуска отличается от даты
    /// приказа: подписывают заранее, а действует с понедельника.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>По какую дату — у отпуска и командировки.</summary>
    public DateOnly? EffectiveTo { get; set; }

    public required string Title { get; set; }

    /// <summary>Текст приказа — распорядительная часть.</summary>
    public string? Body { get; set; }

    /// <summary>
    /// Основание: служебная записка, заявление, решение органа. Приказ без
    /// основания — то, что первым спрашивает проверка.
    /// </summary>
    public string? Basis { get; set; }

    /// <summary>Служебная записка, по которой издан приказ.</summary>
    public int? SourceSzId { get; set; }

    /// <summary>Приказ, который этот отменяет или изменяет.</summary>
    public int? CancelsOrderId { get; set; }
    public HrOrder? CancelsOrder { get; set; }

    /// <summary>Подписант — руководитель, издающий приказ.</summary>
    public int? SignerUserId { get; set; }
    public User? SignerUser { get; set; }

    public DateTime? SignedAt { get; set; }

    /// <summary>
    /// Лист ознакомления. Заводится при подписании: сотрудник должен быть ознакомлен
    /// с приказом под роспись, и механизм для этого в системе уже есть.
    /// </summary>
    public int? AcknowledgementSheetId { get; set; }

    public int? NomenclatureCaseId { get; set; }
    public NomenclatureCase? NomenclatureCase { get; set; }

    /// <summary>Сотрудники, которых приказ касается. Их может быть несколько.</summary>
    public ICollection<HrOrderEmployee> Employees { get; set; } = new List<HrOrderEmployee>();

    /// <summary>
    /// Поисковый образ приказа: заголовок, текст, основание, номер.
    ///
    /// Приказ по личному составу содержит оклады и взыскания, поэтому в поиске он
    /// закрыт тем же правилом, что и книга приказов: кадровая служба видит всё,
    /// остальные — только приказы о себе.
    /// </summary>
    public NpgsqlTypes.NpgsqlTsVector? SearchVector { get; set; }

    /// <summary>Сканы подписанного приказа и приложений к нему.</summary>
    public ICollection<HrOrderFile> Files { get; set; } = new List<HrOrderFile>();

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Сотрудник в приказе.
///
/// Отдельной записью, а не полем: в командировку едут группой, оклад повышают
/// отделу, премируют список. Реквизиты по каждому свои — у одного одна должность,
/// у другого другая.
/// </summary>
public class HrOrderEmployee
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public HrOrder? Order { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>ФИО на момент издания — фамилия сотрудника может измениться позже.</summary>
    public string? FullNameSnapshot { get; set; }

    /// <summary>Должность и подразделение на момент издания приказа.</summary>
    public string? PositionSnapshot { get; set; }
    public string? UnitSnapshot { get; set; }

    /// <summary>
    /// Реквизиты, зависящие от вида приказа: город и цель у командировки, суммы
    /// у изменения оклада, вид и период у отпуска. Хранятся как JSON — набор полей
    /// задаётся видом, а не структурой таблицы.
    /// </summary>
    public string? FieldValues { get; set; }
}

/// <summary>
/// Скан подписанного приказа.
///
/// Приказ по личному составу подписывают на бумаге и хранят в личном деле;
/// без скана карточка в системе оставалась записью о приказе, а не самим
/// приказом — и на вопрос «покажите подписанный» ответить было нечем.
/// </summary>
public class HrOrderFile
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public HrOrder? Order { get; set; }

    public int FileId { get; set; }
    public Files.Models.FileAttachment? File { get; set; }

    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
