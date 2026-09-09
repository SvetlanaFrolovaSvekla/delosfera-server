namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Постраничный результат — сейчас используется только историей "Выполнено" на
/// странице "Мои задачи" (см. TasksService.Get*DoneTasksAsync), где полный список без пагинации
/// мог бы стать длинным на давно активных документах/пользователях.</summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore => (long)Page * PageSize < TotalCount;
}
