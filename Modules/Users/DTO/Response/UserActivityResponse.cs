namespace delosfera_server.Modules.Users.DTO.Response;

/// <summary>Одно действие пользователя в ленте активности.</summary>
public class UserActivityItemResponse
{
    /// <summary>Тип: "vnd_created" | "approval_decided" | "approval_initiated".</summary>
    public required string Type { get; set; }

    /// <summary>Точный вид действия для цвета точки и фильтра (см. UserActivityKinds):
    /// created | processStarted | approved | approvedWithComment | rejected | autoApproved | removed | other.</summary>
    public required string Kind { get; set; }

    public int? VndId { get; set; }
    public string? VndCode { get; set; }
    public string? VndTitle { get; set; }
    public required DateTime Timestamp { get; set; }
    public required string Description { get; set; }
}

/// <summary>Лента активности пользователя: счётчики + страница действий.</summary>
public class UserActivityResponse
{
    public int VndCreatedCount { get; set; }
    public int ApprovalsDecidedCount { get; set; }
    public int ApprovalsInitiatedCount { get; set; }

    /// <summary>Действия текущей страницы, новые сверху.</summary>
    public List<UserActivityItemResponse> Recent { get; set; } = [];

    /// <summary>Есть ли ещё записи после этой страницы.</summary>
    public bool HasMore { get; set; }

    /// <summary>Курсор для следующей страницы (передать в ?cursor=), null — дальше ничего нет.</summary>
    public string? NextCursor { get; set; }

    /// <summary>Сколько записей каждого вида подходит под период и поиск (без учёта фильтра по видам) —
    /// для легенды-фильтра. Считается только для первой страницы, на следующих — null.</summary>
    public Dictionary<string, int>? KindCounts { get; set; }
}
