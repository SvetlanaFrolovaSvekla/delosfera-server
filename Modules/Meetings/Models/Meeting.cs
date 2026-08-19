using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Files.Models;
using delosfera_server.Modules.Users.Models;

using NpgsqlTypes;

namespace delosfera_server.Modules.Meetings.Models;

/// <summary>
/// Заседание коллегиального органа (ТЗ «Исполнение решений КПА, Правления и Комитетов»).
///
/// Заседание — это шапка: когда, в какой форме и кто секретарь. Содержание живёт
/// в вопросах повестки: именно у вопроса есть протокол, решение и поручения, поэтому
/// сроки и отчёты об исполнении привязаны к вопросу, а не к заседанию целиком.
/// </summary>
public class Meeting : IAuditableEntity
{
    public int Id { get; set; }

    public MeetingBody Body { get; set; }

    /// <summary>
    /// Порядковый номер заседания. Счётчик обнуляется 1 января и ведётся отдельно
    /// по каждому органу, поэтому уникальность — по тройке (год, орган, номер).
    /// </summary>
    public int Number { get; set; }

    /// <summary>Год заседания. Хранится отдельно от даты: по нему работает счётчик номеров.</summary>
    public int Year { get; set; }

    public MeetingForm Form { get; set; } = MeetingForm.InPerson;

    /// <summary>Секретарь заседания. По умолчанию — тот, кто создаёт запись.</summary>
    public int SecretaryUserId { get; set; }
    public User? Secretary { get; set; }

    /// <summary>Управление секретаря.</summary>
    public int? SecretaryUnitId { get; set; }
    public OrganizationUnit? SecretaryUnit { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }

    /// <summary>Ссылка на материалы заседания — подставляется в текст уведомления.</summary>
    public string? MaterialsUrl { get; set; }

    /// <summary>Когда участникам было отправлено уведомление о заседании.</summary>
    public DateTime? NotifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AgendaItem> Items { get; set; } = new List<AgendaItem>();
}

/// <summary>
/// Вопрос повестки дня заседания: тема, принятое решение и поручения по нему.
/// </summary>
public class AgendaItem : IAuditableEntity
{
    public int Id { get; set; }

    public int MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    /// <summary>Порядок вопроса в повестке.</summary>
    public int Order { get; set; }

    public required string Topic { get; set; }

    /// <summary>Поисковый вектор по теме, решению и номеру протокола (GEN-04).</summary>
    public NpgsqlTsVector? SearchVector { get; set; }

    /// <summary>
    /// Номер протокола по маске [гггг-хх-х]: год, порядковый номер заседания и признак,
    /// который для КПА остаётся пустым. Номер редактируемый — часть протоколов приходит
    /// из внешнего учёта со своей нумерацией.
    /// </summary>
    public string? ProtocolNumber { get; set; }

    /// <summary>Дата протокола — подставляется в тексты напоминаний об исполнении.</summary>
    public DateOnly? ProtocolDate { get; set; }

    /// <summary>Принятые решения.</summary>
    public string? Decision { get; set; }

    public int? SpeakerUserId { get; set; }
    public User? Speaker { get; set; }

    /// <summary>Руководитель докладчика — получает уведомления наравне с докладчиком.</summary>
    public int? SpeakerHeadUserId { get; set; }
    public User? SpeakerHead { get; set; }

    /// <summary>Управление докладчика.</summary>
    public int? SpeakerUnitId { get; set; }
    public OrganizationUnit? SpeakerUnit { get; set; }

    /// <summary>Дублёр секретаря.</summary>
    public int? DeputySecretaryUserId { get; set; }
    public User? DeputySecretary { get; set; }

    /// <summary>Контроль исполнения.</summary>
    public int? ControllerUserId { get; set; }
    public User? Controller { get; set; }

    /// <summary>Ссылка на документы.</summary>
    public string? DocumentsUrl { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AgendaGuest> Guests { get; set; } = new List<AgendaGuest>();
    public ICollection<AgendaAssignment> Assignments { get; set; } = new List<AgendaAssignment>();
    public ICollection<AgendaFile> Files { get; set; } = new List<AgendaFile>();
}

/// <summary>Приглашённое на рассмотрение вопроса лицо.</summary>
public class AgendaGuest
{
    public int Id { get; set; }

    public int AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }
}

/// <summary>
/// Поручение по вопросу повестки: кто, к какому сроку и с каким результатом.
///
/// Отчёт об исполнении и статус заполняет исполняющее подразделение, а не секретарь —
/// это единственные поля вопроса, которые доступны им на изменение.
/// </summary>
public class AgendaAssignment : IAuditableEntity
{
    public int Id { get; set; }

    public int AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    /// <summary>
    /// Что именно поручено. По одному вопросу поручений бывает несколько — разным
    /// людям, с разными сроками и разными формулировками; без текста исполнитель
    /// видит только срок и гадает, что от него хотят.
    ///
    /// Пусто — поручение относится к решению по вопросу целиком.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>Срок исполнения. От него считаются напоминания за 5 дней, в день срока и после просрочки.</summary>
    public DateOnly? DueDate { get; set; }

    public ExecutionStatus Status { get; set; } = ExecutionStatus.New;

    /// <summary>Отчёт об исполнении — доступен на заполнение исполнителю.</summary>
    public string? Report { get; set; }

    public DateTime? ReportedAt { get; set; }
    public int? ReportedByUserId { get; set; }

    /// <summary>
    /// Дата последнего отправленного напоминания. Хранится, чтобы фоновая рассылка
    /// не повторяла одно и то же письмо при каждом запуске в течение дня.
    /// </summary>
    public DateOnly? LastReminderOn { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Файл, приложенный к вопросу повестки: СЗ, протокол или документ об исполнении.</summary>
public class AgendaFile
{
    public int Id { get; set; }

    public int AgendaItemId { get; set; }
    public AgendaItem? AgendaItem { get; set; }

    public MeetingFileKind Kind { get; set; }

    public int FileId { get; set; }
    public FileAttachment? File { get; set; }

    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
