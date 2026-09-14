namespace delosfera_server.Modules.Analytics.DTO.Response.Sla;

/// <summary>
/// Сводка соблюдения сроков (SLA) по всем контурам сразу (СК-2).
///
/// Считает открытые задачи всей организации — согласования записок и закупок,
/// согласование ВНД и листы ознакомления — и долю тех, у кого срок ещё не нарушен.
/// Руководителю нужна не своя очередь (это рабочий стол), а картина по банку: где
/// сроки горят и в каком контуре.
/// </summary>
public class SlaOverviewResponse
{
    /// <summary>Всего открытых задач со сроком и без.</summary>
    public int OpenTasks { get; set; }

    /// <summary>Из них срок уже нарушен.</summary>
    public int Overdue { get; set; }

    /// <summary>Срок истекает в ближайшие 24 часа (ещё не нарушен).</summary>
    public int DueSoon { get; set; }

    /// <summary>Доля задач в срок, %: (открытые − просроченные) / открытые.</summary>
    public double CompliancePercent { get; set; }

    /// <summary>Разбивка по контурам.</summary>
    public List<SlaContourRow> Contours { get; set; } = [];
}

/// <summary>Строка разбивки SLA по одному контуру.</summary>
public class SlaContourRow
{
    /// <summary>Код контура: Sz | Procurement | Vnd | Acknowledgement.</summary>
    public required string Contour { get; set; }

    /// <summary>Человекочитаемое название контура.</summary>
    public required string Label { get; set; }

    public int Open { get; set; }
    public int Overdue { get; set; }
    public int DueSoon { get; set; }

    /// <summary>Доля задач в срок в этом контуре, %.</summary>
    public double CompliancePercent { get; set; }
}

/// <summary>Сотрудник с наибольшим числом просроченных задач по всем контурам.</summary>
public class SlaViolatorItem
{
    public int UserId { get; set; }
    public required string FullName { get; set; }
    public string? OrgUnitLabel { get; set; }

    /// <summary>Открытых задач всего.</summary>
    public int Open { get; set; }

    /// <summary>Из них просрочено.</summary>
    public int Overdue { get; set; }
}
