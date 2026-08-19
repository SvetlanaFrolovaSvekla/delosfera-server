namespace delosfera_server.Common.Models;

/// <summary>
/// Страница результатов + общее количество (для серверной пагинации).
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];

    /// <summary>Общее число записей, удовлетворяющих фильтрам (без учёта страницы).</summary>
    public int Total { get; set; }

    public int Page { get; set; }
    public int PageSize { get; set; }
}
