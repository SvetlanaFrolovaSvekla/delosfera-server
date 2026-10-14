namespace delosfera_server.Modules.ActivityLog.DTO.Request;

/// <summary>Отбор для страницы "Активность на портале" (GET api/activity-log/feed): поиск, период,
/// тип события, раздел и постраничная подгрузка ("Показать ещё").</summary>
public class ActivityFeedRequest
{
    /// <summary>Раздел: vnd | sz | prc. Пусто — все.</summary>
    public string? Module { get; set; }

    /// <summary>Поиск: слова через пробел, запись подходит, если в тексте есть ВСЕ слова
    /// (подстрокой, без учёта регистра).</summary>
    public string? Q { get; set; }

    /// <summary>Не раньше (включительно).</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Не позже (включительно).</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Типы события через запятую — те же значки, что у записей:
    /// check, x, doc, clock, edit, trash, info. Пусто — все.</summary>
    public string? Icons { get; set; }

    /// <summary>Курсор следующей страницы — NextCursor из прошлого ответа.</summary>
    public string? Cursor { get; set; }

    public int Limit { get; set; } = 30;
}
