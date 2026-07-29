namespace delosfera_server.Modules.Vnd.DTO.Request;

/// <summary>Фильтр по дате — точная или диапазон</summary>
public class DateRangeFilter
{
    public DateOnly? Exact { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}