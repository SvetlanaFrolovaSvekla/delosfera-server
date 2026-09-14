namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>
/// Колонка доски закупок (ЗК-11): одна стадия жизненного цикла и заявки на ней.
/// Руководителю закупок нужна не таблица со статусом в ячейке, а картина «где что
/// стоит»: сколько заявок на согласовании, сколько в процедуре, что зависло.
/// </summary>
public class ProcurementTrackerColumnDto
{
    public required string Code { get; set; }
    public required string Title { get; set; }
    public int Count { get; set; }

    /// <summary>Суммарная стоимость заявок в колонке.</summary>
    public decimal TotalAmount { get; set; }

    public List<ProcurementTrackerItemDto> Items { get; set; } = [];
}

/// <summary>Заявка на доске.</summary>
public class ProcurementTrackerItemDto
{
    public int Id { get; set; }
    public string? RegNumber { get; set; }
    public required string Subject { get; set; }
    public decimal Amount { get; set; }
    public string? InitiatorUnit { get; set; }
    public string? CuratorName { get; set; }

    /// <summary>Сколько дней заявка живёт (от создания).</summary>
    public int AgeDays { get; set; }

    /// <summary>Заявка висит дольше норматива и ещё не завершена — повод разобраться.</summary>
    public bool IsStale { get; set; }
}
