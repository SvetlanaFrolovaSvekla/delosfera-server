namespace delosfera_server.Modules.Analytics.DTO;

/// <summary>
/// Событие календаря сроков (ЗС-13): датированная задача пользователя, размещаемая на
/// сетке месяца. Календарь отвечает на «когда что», реестр — на «что есть»: одно и то
/// же множество задач, но разложенное по датам, чтобы увидеть загрузку и заторы во
/// времени, а не одним списком.
/// </summary>
public class CalendarEventDto
{
    /// <summary>День срока (по нему событие ложится в ячейку календаря).</summary>
    public DateOnly Date { get; set; }

    public int? EntityId { get; set; }
    public required string DocumentType { get; set; }
    public required string DocumentTypeTitle { get; set; }
    public string? RegNumber { get; set; }
    public required string Title { get; set; }
    public required string TaskType { get; set; }
    public bool IsOverdue { get; set; }

    /// <summary>Точное время срока — для подсказки в ячейке.</summary>
    public DateTime DueAt { get; set; }
}
