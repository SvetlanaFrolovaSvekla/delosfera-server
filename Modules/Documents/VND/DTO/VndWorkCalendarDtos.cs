namespace delosfera_server.Modules.Documents.VND.DTO;

/// <summary>Праздничный день справочника "Производственный календарь".</summary>
public class VndWorkCalendarDayResponse
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class SaveVndWorkCalendarDayRequest
{
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class CopyVndWorkCalendarYearRequest
{
    public int FromYear { get; set; }
    public int ToYear { get; set; }
}

public class CopyVndWorkCalendarYearResponse
{
    public int Added { get; set; }
    public int Skipped { get; set; }
}

/// <summary>Рабочее время банка: минуты от полуночи по Бишкеку (09:00 = 540).</summary>
public class VndWorkHoursResponse
{
    public int WorkStartMinutes { get; set; }
    public int WorkEndMinutes { get; set; }
}

public class UpdateVndWorkHoursRequest
{
    public int WorkStartMinutes { get; set; }
    public int WorkEndMinutes { get; set; }
}

/// <summary>Всё, что нужно клиенту, чтобы самому показывать "осталось N рабочих часов" и
/// предпросмотр срока без запроса на каждый тик: рабочие часы банка и праздники на период.</summary>
public class VndWorkCalendarRulesResponse
{
    public string TimeZone { get; set; } = "Asia/Bishkek";
    public int UtcOffsetMinutes { get; set; }
    public int WorkStartMinutes { get; set; }
    public int WorkEndMinutes { get; set; }
    /// <summary>Длина рабочего дня — «1 д.» в нормативах согласования.</summary>
    public int WorkDayMinutes { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<DateOnly> Holidays { get; set; } = [];
}
