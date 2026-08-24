using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Correspondence.Models;

/// <summary>Куда идёт письмо.</summary>
public enum LetterDirection
{
    Incoming = 1,
    Outgoing = 2,
}

/// <summary>
/// Категория письма. От неё зависит срок ответа и круг допущенных.
///
/// Одна сущность на все виды переписки намеренно: письмо из НБКР, жалоба клиента и
/// счёт от поставщика различаются сроками и доступом, но не устройством — у всех
/// есть отправитель, номер, срок и ответ. Разводить их по отдельным модулям значит
/// трижды написать одну и ту же книгу регистрации, а потом искать письмо в трёх
/// местах.
/// </summary>
public enum LetterCategory
{
    /// <summary>Обычная деловая переписка.</summary>
    Ordinary = 0,

    /// <summary>
    /// Запрос или предписание Национального банка. Срок указан в самом документе,
    /// нарушение — основание для мер надзорного реагирования.
    /// </summary>
    RegulatorRequest = 1,

    /// <summary>
    /// Обращение или жалоба клиента. Срок рассмотрения установлен законом, и
    /// просрочка — нарушение прав потребителя финансовых услуг.
    /// </summary>
    ClientAppeal = 2,

    /// <summary>
    /// Запрос государственного органа по счетам и операциям. Банковская тайна:
    /// узкий круг допущенных, короткий срок, полный след в журнале.
    /// </summary>
    BankSecrecyInquiry = 3,

    /// <summary>Претензия или иск — идёт в юридическое управление.</summary>
    Claim = 4,
}

/// <summary>Состояние письма.</summary>
public enum LetterStatus
{
    /// <summary>Проект исходящего — ещё не подписан и не отправлен.</summary>
    Draft = 0,

    /// <summary>Зарегистрировано в книге.</summary>
    Registered = 1,

    /// <summary>У руководителя на резолюцию.</summary>
    OnResolution = 2,

    /// <summary>Исполняется: готовится ответ или выполняется поручение.</summary>
    OnExecution = 3,

    /// <summary>Ответ отправлен.</summary>
    Answered = 4,

    /// <summary>Закрыто без ответа — ответ не требовался.</summary>
    Closed = 5,

    /// <summary>Исходящее отправлено адресату.</summary>
    Sent = 6,
}

/// <summary>Как письмо пришло или ушло.</summary>
public enum DeliveryMethod
{
    Post = 1,
    Courier = 2,
    Email = 3,

    /// <summary>Межведомственное взаимодействие «Тундук».</summary>
    Tunduk = 4,

    Handed = 5,
    Fax = 6,
    Other = 9,
}

/// <summary>
/// Письмо: входящее или исходящее.
///
/// Книга регистрации — то, чего в системе не было вовсе, а в банке она есть всегда:
/// письмо из НБКР со сроком в десять дней, жалоба клиента с законным сроком
/// рассмотрения, запрос суда по счетам. Пока они живут в почте и бумажной папке,
/// вопрос «что у нас просрочено» задают человеку, а не системе.
/// </summary>
public class CorrespondenceLetter : IAuditableEntity
{
    public int Id { get; set; }

    public LetterDirection Direction { get; set; }
    public LetterCategory Category { get; set; } = LetterCategory.Ordinary;

    // --- регистрация ---

    /// <summary>
    /// Регистрационный номер в книге. Книги входящих и исходящих раздельные, и
    /// нумерация в каждой своя — как на бумаге.
    /// </summary>
    public string? RegNumber { get; set; }

    public DateOnly? RegisteredOn { get; set; }
    public int Year { get; set; }
    public int? RegisteredByUserId { get; set; }

    // --- корреспондент ---

    public int CorrespondentId { get; set; }
    public Correspondent? Correspondent { get; set; }

    /// <summary>Номер, присвоенный письму отправителем. У входящих — их исходящий.</summary>
    public string? TheirNumber { get; set; }

    /// <summary>Дата письма по документу отправителя.</summary>
    public DateOnly? TheirDate { get; set; }

    // --- содержание ---

    public required string Subject { get; set; }

    /// <summary>Краткое изложение — чтобы понять суть, не открывая вложение.</summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Поисковый вектор по теме, изложению и номерам. Вычисляемая колонка Postgres:
    /// поле, которое надо помнить обновлять, рано или поздно разойдётся с данными.
    /// </summary>
    public NpgsqlTypes.NpgsqlTsVector? SearchVector { get; set; }

    public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Post;

    /// <summary>Число листов и приложений — реквизит книги регистрации.</summary>
    public int? SheetCount { get; set; }
    public string? Enclosures { get; set; }

    // --- работа с письмом ---

    public LetterStatus Status { get; set; } = LetterStatus.Registered;

    /// <summary>Резолюция руководителя: кому и что делать.</summary>
    public string? Resolution { get; set; }
    public DateTime? ResolutionAt { get; set; }
    public int? ResolutionByUserId { get; set; }
    public User? ResolutionByUser { get; set; }

    public int? ResponsibleUserId { get; set; }
    public User? ResponsibleUser { get; set; }

    public int? ResponsibleUnitId { get; set; }
    public OrganizationUnit? ResponsibleUnit { get; set; }

    /// <summary>
    /// Срок ответа. Для запросов регулятора берётся из самого документа, для
    /// обращений клиентов — считается по закону от даты регистрации.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>На контроле — за сроком следит делопроизводство.</summary>
    public bool IsControlled { get; set; }

    /// <summary>Отметка об исполнении: чем закончилось.</summary>
    public string? ExecutionNote { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public int? ExecutedByUserId { get; set; }

    // --- связи ---

    /// <summary>
    /// Письмо, на которое это отвечает. Исходящее ссылается на входящее — так
    /// закрывается срок: ответ есть, и видно, каким письмом.
    /// </summary>
    public int? InReplyToId { get; set; }
    public CorrespondenceLetter? InReplyTo { get; set; }

    /// <summary>Ответы на это письмо.</summary>
    public ICollection<CorrespondenceLetter> Replies { get; set; } = new List<CorrespondenceLetter>();

    /// <summary>Служебная записка, которой инициировано исходящее письмо.</summary>
    public int? SourceSzId { get; set; }

    /// <summary>Номенклатурное дело для архивного хранения.</summary>
    public int? NomenclatureCaseId { get; set; }
    public NomenclatureCase? NomenclatureCase { get; set; }

    public ICollection<LetterFile> Files { get; set; } = new List<LetterFile>();

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Просрочено ли. Считается, а не хранится: срок и факт ответа — оба уже здесь,
    /// а вторая запись об этом же расходилась бы с ними при каждой правке даты.
    /// </summary>
    public bool IsOverdue(DateOnly today) =>
        DueDate is {} due
        && Status is not (LetterStatus.Answered or LetterStatus.Closed or LetterStatus.Sent)
        && due < today;
}

/// <summary>Файл письма: скан оригинала, приложение, проект ответа.</summary>
public class LetterFile
{
    public int Id { get; set; }

    public int LetterId { get; set; }
    public CorrespondenceLetter? Letter { get; set; }

    public int FileId { get; set; }
    public Files.Models.FileAttachment? File { get; set; }

    /// <summary>
    /// Хеш файла на момент загрузки. Скан бумажного документа признаётся
    /// доказательством, если установлено, кто его загрузил и что он не менялся;
    /// автора пишет журнал, неизменность — этот хеш.
    /// </summary>
    public string? ContentHash { get; set; }

    public int UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
