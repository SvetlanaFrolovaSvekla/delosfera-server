namespace delosfera_server.Modules.Analytics.DTO.Response;

/// <summary>Универсальная точка данных "категория - значение" для круговых/столбчатых диаграмм
/// (распределение по статусам, типам, подразделениям, ролям и т.п.)</summary>
public class ChartCategoryPoint
{
    /// <summary>Id связанной сущности (типа ВНД, подразделения, роли...), если применимо -
    /// удобно для перехода/фильтрации по клику на сегмент диаграммы</summary>
    public int? Id { get; set; }

    /// <summary>Подпись категории для отображения на графике (уже на нужном языке)</summary>
    public required string Label { get; set; }

    /// <summary>Значение (количество)</summary>
    public int Value { get; set; }

    /// <summary>Доля от общего количества в процентах (0-100), для удобства фронта</summary>
    public double Percent { get; set; }
}
