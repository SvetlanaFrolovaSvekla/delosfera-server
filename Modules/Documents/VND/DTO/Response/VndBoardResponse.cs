namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Подразделение, по которому собрана "Доска ВНД".</summary>
public class VndBoardUnitResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Документ на "Доске ВНД" и причины, по которым он туда попал.</summary>
public class VndBoardEntryResponse
{
    public required VndResponse Vnd { get; set; }

    /// <summary>"developer" — СП пользователя разработчик документа; "executor" — СП пользователя
    /// среди ответственных исполнителей; "pinned" — документ добавлен на доску вручную.</summary>
    public List<string> Reasons { get; set; } = [];

    /// <summary>Когда документ добавлен вручную (только для "pinned").</summary>
    public DateTime? PinnedAt { get; set; }
}

/// <summary>GET api/vnd/board</summary>
public class VndBoardResponse
{
    /// <summary>Подразделение пользователя (как указано в его карточке). Null — пользователь не
    /// привязан к подразделению: на доске тогда только добавленные вручную документы.</summary>
    public VndBoardUnitResponse? OwnUnit { get; set; }

    /// <summary>"Домашнее" СП, по которому подбираются документы, — ближайшее управление/департамент
    /// над подразделением пользователя (или само подразделение, если выше управления нет).</summary>
    public VndBoardUnitResponse? ScopeUnit { get; set; }

    /// <summary>Сколько подразделений (вместе с вложенными) входит в ScopeUnit.</summary>
    public int ScopeUnitCount { get; set; }

    public List<VndBoardEntryResponse> Items { get; set; } = [];

    /// <summary>Связанные с СП документы, которые пользователь убрал с доски — их можно вернуть.</summary>
    public List<VndResponse> Hidden { get; set; } = [];

    /// <summary>Личное "Избранное" пользователя (звёздочки) — отдельная панель доски.</summary>
    public List<VndResponse> Favorites { get; set; } = [];
}
