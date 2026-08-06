namespace delosfera_server.Modules.Analytics.DTO.Response.Vnd;

/// <summary>Точка графика динамики жизненного цикла ВНД за период (сколько создано/опубликовано/архивировано)</summary>
public class VndDynamicsPoint
{
    /// <summary>Начало периода</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Подпись периода для оси X</summary>
    public required string PeriodLabel { get; set; }

    /// <summary>Создано новых ВНД (по CreatedAt документа)</summary>
    public int Created { get; set; }

    /// <summary>Опубликовано редакций / завершено циклов актуализации (стали действующими)</summary>
    public int Published { get; set; }

    /// <summary>Отправлено на согласование редакций</summary>
    public int SentToApproval { get; set; }

    /// <summary>Архивировано документов</summary>
    public int Archived { get; set; }
}
