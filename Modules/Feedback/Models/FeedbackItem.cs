namespace delosfera_server.Modules.Feedback.Models;

/// <summary>
/// О чём сообщение. Три вида, а не пять: чем длиннее список, тем дольше человек
/// выбирает и тем чаще ошибается. Разбирающему всё равно нужно прочитать текст.
/// </summary>
public enum FeedbackKind
{
    /// <summary>Не работает: ошибка, пустой экран, действие не проходит.</summary>
    Problem = 1,

    /// <summary>Пожелание: работает, но неудобно или чего-то не хватает.</summary>
    Wish = 2,

    /// <summary>Непонятно: человек не разобрался, что делать на экране.</summary>
    Question = 3,
}

/// <summary>
/// Что с сообщением сделали. «Отклонено» существует наравне с «принято»: обкатка
/// приносит и то, что делать не будут, и оставлять такие сообщения вечно новыми —
/// значит потерять из виду настоящие.
/// </summary>
public enum FeedbackStatus
{
    New = 0,
    InProgress = 1,
    Accepted = 2,
    Declined = 3,
    Done = 4,
}

/// <summary>
/// Сообщение от сотрудника с той страницы, где он находится.
///
/// Смысл в привязке к экрану. Обкатку ведут подразделения, для которых система —
/// не работа, а помеха работе; писать письмо с описанием, где именно и что именно
/// не так, они не станут. Кнопка на странице снимает этот порог: контекст —
/// маршрут, заголовок, браузер, размер окна — система записывает сама.
///
/// Хранится отдельно от журнала действий намеренно. Журнал отвечает на вопрос
/// «что произошло с документом», а это — «что человек думает о системе»;
/// смешивать их значит засорить оба.
/// </summary>
public class FeedbackItem
{
    public long Id { get; set; }

    public FeedbackKind Kind { get; set; }

    /// <summary>Текст сообщения. Единственное, что заполняет человек.</summary>
    public required string Text { get; set; }

    /// <summary>
    /// Маршрут экрана в виде шаблона: «/base-vnd/:id», а не «/base-vnd/17».
    /// Так все сообщения об одном экране собираются вместе, даже если каждый
    /// писал о своём документе.
    /// </summary>
    public required string RoutePath { get; set; }

    /// <summary>Идентификатор объекта из адреса, если он там был. Нужен, чтобы открыть тот самый документ.</summary>
    public int? EntityId { get; set; }

    /// <summary>Заголовок страницы, каким его видел человек.</summary>
    public string? PageTitle { get; set; }

    public int UserId { get; set; }
    public Users.Models.User? User { get; set; }

    /// <summary>
    /// Браузер и размер окна. Половина сообщений вида «кнопка не помещается»
    /// объясняется именно этим, и спрашивать потом отдельно — терять день.
    /// </summary>
    public string? UserAgent { get; set; }
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }

    public DateTime CreatedAt { get; set; }

    public FeedbackStatus Status { get; set; }

    /// <summary>Ответ разбирающего. Виден автору сообщения — иначе он не узнает, что его услышали.</summary>
    public string? HandlerComment { get; set; }

    public int? HandledByUserId { get; set; }
    public Users.Models.User? HandledByUser { get; set; }
    public DateTime? HandledAt { get; set; }
}
