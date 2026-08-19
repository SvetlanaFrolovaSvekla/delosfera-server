namespace delosfera_server.Modules.Sz.DTO;

public class SzProcurementHandoffRequest
{
    /// <summary>Предмет закупки: чем будет называться заявка.</summary>
    public string? Subject { get; set; }

    /// <summary>Комментарий делопроизводства к передаче.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// Состояние передачи записки в закупочный контур (PRC-01).
/// Пока модуль закупок не реализован, заявка существует как документ-заготовка
/// в едином реестре: контур подхватит её, когда появится.
/// </summary>
public class SzProcurementResponse
{
    public int SzId { get; set; }
    public string? SzRegNumber { get; set; }

    /// <summary>Записка относится к виду «на закупку».</summary>
    public bool IsProcurementKind { get; set; }

    /// <summary>Реквизиты, которые уходят в заявку.</summary>
    public bool? HasBudget { get; set; }
    public decimal? Amount { get; set; }
    public string? InitiatorName { get; set; }
    public string? InitiatorUnit { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>Закупка уже запущена по этой записке.</summary>
    public bool IsHandedOver { get; set; }
    public DateTime? HandedOverAt { get; set; }

    /// <summary>Документ-заявка в едином реестре.</summary>
    public int? ProcurementDocumentId { get; set;}
    public string? ProcurementRegNumber { get; set; }
    public string? ProcurementTitle { get; set; }
    public string? ProcurementStatus { get; set; }

    /// <summary>Чего не хватает для передачи; пусто — можно передавать.</summary>
    public List<string> Blockers { get; set; } = [];
}
