namespace delosfera_server.Modules.Sz.DTO;

/// <summary>Колонка доски записок (РС-4): стадия и записки на ней.</summary>
public class SzTrackerColumnDto
{
    public required string Code { get; set; }
    public required string Title { get; set; }
    public int Count { get; set; }
    public List<SzTrackerItemDto> Items { get; set; } = [];
}

/// <summary>Доска записок целиком: колонки и пороги, по которым сервер считал «зависла».</summary>
public class SzTrackerBoardDto
{
    public List<SzTrackerColumnDto> Columns { get; set; } = [];

    /// <summary>Записка в ходу считается зависшей, если на текущей стадии дольше стольких дней.</summary>
    public int StaleAfterDays { get; set; }

    /// <summary>То же для записки на исполнении без срока исполнения.</summary>
    public int StaleExecutionAfterDays { get; set; }

    /// <summary>
    /// Есть ли у пользователя подразделение — без него быстрый фильтр «Моё подразделение»
    /// всегда пуст, и показывать его незачем.
    /// </summary>
    public bool HasUnit { get; set; }

    /// <summary>
    /// Видит ли пользователь чужие черновики в реестре (администратор системы). На доске
    /// чужих черновиков нет ни у кого — подсказка к колонке говорит, где их искать.
    /// </summary>
    public bool CanSeeOthersDrafts { get; set; }
}

/// <summary>Записка на доске.</summary>
public class SzTrackerItemDto
{
    public int Id { get; set; }
    public string? RegNumber { get; set; }
    public required string Title { get; set; }

    /// <summary>Вид записки: id — для перевода на язык интерфейса, название — запасной вариант.</summary>
    public int KindId { get; set; }
    public required string Kind { get; set; }

    public string? AuthorName { get; set; }
    public string? AddresseeName { get; set; }

    /// <summary>Сколько дней записка живёт (от создания).</summary>
    public int AgeDays { get; set; }

    /// <summary>С какого момента записка на текущей стадии.</summary>
    public DateTime StageSince { get; set; }

    /// <summary>Сколько календарных дней записка на текущей стадии.</summary>
    public int StageDays { get; set; }

    /// <summary>Записка стоит на стадии дольше порога — повод разобраться.</summary>
    public bool IsStale { get; set; }

    /// <summary>Срок исполнения записки, если задан.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Дней до срока; отрицательное — на столько просрочена. Пусто, если срока нет.</summary>
    public int? DaysLeft { get; set; }

    /// <summary>Срок исполнения прошёл.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>Автор — текущий пользователь (быстрый фильтр «Мои»).</summary>
    public bool IsMine { get; set; }

    /// <summary>Записка подразделения пользователя (быстрый фильтр «Моё подразделение»).</summary>
    public bool IsMyUnit { get; set; }
}
