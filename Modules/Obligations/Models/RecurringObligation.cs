using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Obligations.Models;

/// <summary>Как часто обязательство должно исполняться.</summary>
public enum Periodicity
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    SemiAnnual = 4,
    Annual = 5,
}

/// <summary>
/// Чем обязательство закрывается. От вида зависит, может ли система засчитать
/// исполнение сама или ждёт отметки человека.
/// </summary>
public enum ObligationKind
{
    /// <summary>
    /// Заседание коллегиального органа. Закрывается само: система видит заседание
    /// нужного органа в нужном периоде. Человеку отмечать нечего — заседание либо
    /// заведено, либо нет.
    /// </summary>
    MeetingHeld = 1,

    /// <summary>Отчёт: службы рисков комитету, комитета совету, отчётность в НБКР.</summary>
    ReportSubmitted = 2,

    /// <summary>Пересмотр документа: риск-аппетит, политика, положение о комитете.</summary>
    DocumentReviewed = 3,

    /// <summary>Прочее — закрывается отметкой ответственного.</summary>
    Other = 9,
}

/// <summary>
/// Регулярное обязательство банка.
///
/// Регулятор мыслит периодичностью: комитет по управлению рисками заседает не реже
/// раза в месяц, служба рисков отчитывается комитету ежемесячно и совету
/// ежеквартально, риск-аппетит пересматривается ежегодно. Пока такого понятия в
/// системе нет, соблюдение этих сроков держится на памяти нескольких человек — и
/// обнаруживается на проверке, а не до неё.
///
/// Сущность одна на все случаи намеренно. Заседание комитета, отчёт в НБКР и
/// ежегодный пересмотр политики отличаются только тем, чем закрываются; заводить
/// под каждый свой механизм значит трижды написать один и тот же календарь.
/// </summary>
public class RecurringObligation : IAuditableEntity
{
    public int Id { get; set; }

    public required string Title { get; set; }

    /// <summary>Что именно требуется сделать — формулировка для ответственного.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Основание: пункт положения НБКР или внутреннего документа. Без него
    /// обязательство через полгода читается как чья-то прихоть, и его отменяют.
    /// </summary>
    public string? Basis { get; set; }

    public ObligationKind Kind { get; set; } = ObligationKind.Other;
    public Periodicity Periodicity { get; set; } = Periodicity.Monthly;

    /// <summary>Орган — для обязательств вида «заседание проведено».</summary>
    public MeetingBody? Body { get; set; }

    public int? ResponsibleUserId { get; set; }
    public User? ResponsibleUser { get; set; }

    public int? ResponsibleUnitId { get; set; }
    public OrganizationUnit? ResponsibleUnit { get; set; }

    /// <summary>
    /// Сколько дней после конца периода ещё считается исполнением в срок.
    /// Квартальный отчёт не сдают тридцать первого марта — его готовят в апреле.
    /// </summary>
    public int GraceDays { get; set; }

    /// <summary>С какого дня обязательство действует. Периоды раньше не заводятся.</summary>
    public DateOnly StartsOn { get; set; }

    /// <summary>По какой день. Пусто — бессрочно.</summary>
    public DateOnly? EndsOn { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ObligationPeriod> Periods { get; set; } = new List<ObligationPeriod>();
}

/// <summary>Состояние периода обязательства.</summary>
public enum ObligationPeriodStatus
{
    /// <summary>Период идёт или срок ещё не вышел.</summary>
    Pending = 0,

    /// <summary>Исполнено.</summary>
    Fulfilled = 1,

    /// <summary>Срок вышел, исполнения нет.</summary>
    Missed = 2,

    /// <summary>Снято: в этом периоде не требовалось. Причина обязательна.</summary>
    Waived = 3,
}

/// <summary>
/// Один период регулярного обязательства: январь, первый квартал, год.
///
/// Хранится отдельной записью, а не считается на лету, потому что у периода есть
/// собственная история: кто закрыл, чем, когда и с каким опозданием. Вычисляемый
/// календарь этого не удержит, а на проверке спрашивают именно это.
/// </summary>
public class ObligationPeriod : IAuditableEntity
{
    public int Id { get; set; }

    public int ObligationId { get; set; }
    public RecurringObligation? Obligation { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Крайний срок: конец периода плюс отсрочка.</summary>
    public DateOnly DueDate { get; set; }

    public ObligationPeriodStatus Status { get; set; } = ObligationPeriodStatus.Pending;

    public DateTime? FulfilledAt { get; set; }
    public int? FulfilledByUserId { get; set; }
    public User? FulfilledByUser { get; set; }

    /// <summary>
    /// Чем закрыто: заседание, документ, отчёт. Для заседаний проставляется системой.
    /// </summary>
    public int? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    /// <summary>Пояснение исполнителя или причина снятия периода.</summary>
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Исполнено с нарушением срока. Считается по датам, а не хранится: в отличие
    /// от поручений заседаний, здесь оценки нет — есть факт даты закрытия против
    /// крайнего срока, и он не меняется от того, кто на него смотрит.
    /// </summary>
    public bool IsLate =>
        Status == ObligationPeriodStatus.Fulfilled
        && FulfilledAt is not null
        && DateOnly.FromDateTime(FulfilledAt.Value) > DueDate;
}
