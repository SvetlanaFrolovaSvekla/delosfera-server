namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>Параметры ленты активности пользователя (GET /users/{id}/activity).</summary>
public class UserActivityQuery
{
    /// <summary>Начало периода (включительно), null — без ограничения.</summary>
    public DateTime? From { get; set; }

    /// <summary>Конец периода (включительно), null — без ограничения.</summary>
    public DateTime? To { get; set; }

    /// <summary>Поиск: запись подходит, если в описании, коде или названии ВНД есть ВСЕ слова.</summary>
    public string? Search { get; set; }

    /// <summary>Типы действий (см. UserActivityKinds); пусто — все.</summary>
    public string[]? Kinds { get; set; }

    /// <summary>Курсор следующей страницы (NextCursor из предыдущего ответа); null — первая страница.</summary>
    public string? Cursor { get; set; }

    /// <summary>Размер страницы (1–100).</summary>
    public int PageSize { get; set; } = 20;
}
