namespace delosfera_server.Modules.Substitutions.DTO;

/// <summary>Запрос на изменение норматива срока согласования (ЗМ-SLA).</summary>
public class SubstitutionSlaRequest
{
    public int ApprovalStepSlaDays { get; set; }
}

/// <summary>Фильтр статистики по заявкам на замещение (ЗМ-SLA). Период — по дате создания заявки.</summary>
public class SubstitutionStatisticsFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>Разбивка «всего / просрочено» по одному измерению (статус, причина, месяц).</summary>
public class SubstitutionStatBucket
{
    public int Total { get; set; }
    public int Overdue { get; set; }
}

/// <summary>Сводка по заявкам на замещение.</summary>
public class SubstitutionStatisticsDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public int Total { get; set; }

    /// <summary>На согласовании прямо сейчас.</summary>
    public int OnApproval { get; set; }

    /// <summary>Из них с превышением норматива срока согласования (ЗМ-SLA).</summary>
    public int Overdue { get; set; }

    public int OnExecution { get; set; }
    public int Executed { get; set; }
    public int Rejected { get; set; }
    public int Withdrawn { get; set; }
    public int Draft { get; set; }

    /// <summary>Норматив срока согласования на этап, рабочих дней.</summary>
    public int SlaDays { get; set; }

    /// <summary>Среднее время прохождения маршрута согласования, календарных дней (по завершённым).</summary>
    public double AvgApprovalDays { get; set; }

    public Dictionary<string, SubstitutionStatBucket> ByReason { get; set; } = new();
    public Dictionary<string, SubstitutionStatBucket> ByMonth { get; set; } = new();
}
