namespace delosfera_server.Modules.Analytics.DTO.Response.ProcessMining;

/// <summary>Доступные значения фильтров для дашборда process mining.</summary>
public class ProcessMiningFilterOptions
{
    /// <summary>Типы документов, по которым есть запущенные маршруты.</summary>
    public required List<ProcessMiningOption> DocumentTypes { get; set; }

    /// <summary>Шаблоны маршрутов, встречающиеся в выборке.</summary>
    public required List<ProcessMiningOption> Templates { get; set; }
}

/// <summary>Пара «значение фильтра — подпись».</summary>
public class ProcessMiningOption
{
    public int Id { get; set; }
    public required string Label { get; set; }
}
