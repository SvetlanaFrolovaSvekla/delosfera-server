namespace delosfera_server.Modules.Analytics.DTO.Request;

/// <summary>
/// Фильтр для process mining по трейсам маршрутов согласования (MINE-1).
/// Все поля необязательны — пустой фильтр берёт все запущенные маршруты.
/// </summary>
public class ProcessMiningFilter
{
    /// <summary>Тип документа (Documents.DocumentType как число): 1 — СЗ, 2 — ВНД и т.д. null — все типы.</summary>
    public int? DocumentType { get; set; }

    /// <summary>Шаблон маршрута (RouteTemplate.Id). null — маршруты по всем шаблонам и без шаблона.</summary>
    public int? TemplateId { get; set; }

    /// <summary>Начало периода (включительно) по дате старта маршрута. null — с самого раннего.</summary>
    public DateOnly? DateFrom { get; set; }

    /// <summary>Конец периода (включительно) по дате старта маршрута. null — по текущий момент.</summary>
    public DateOnly? DateTo { get; set; }

    /// <summary>
    /// Считать длительности в рабочих часах банка (пн–пт, 09:00–18:00) вместо астрономических.
    /// По умолчанию включено: узкие места ищем в рабочем времени, а не в календарном.
    /// </summary>
    public bool BusinessHours { get; set; } = true;
}
