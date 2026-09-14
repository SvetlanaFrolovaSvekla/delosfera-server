namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Подробная сводка по решениям, зачтённым текущему пользователю по тайм-ауту
/// (просрочка согласования) — для блока "Мои показатели" в Аналитике (ВНД → Актуализация).
/// В отличие от VndHomeSummaryResponse.MyTimeoutApprovalsThisMonth (только текущий месяц, для
/// карточки на главной), здесь три среза и список конкретных ВНД.</summary>
public class VndMyTimeoutApprovalsResponse
{
    /// <summary>Просрочек в текущем календарном месяце (по календарю банка)</summary>
    public int ThisMonthCount { get; set; }

    /// <summary>Просрочек в текущем календарном году (по календарю банка)</summary>
    public int ThisYearCount { get; set; }

    /// <summary>Просрочек за всё время</summary>
    public int TotalCount { get; set; }

    /// <summary>Конкретные ВНД и этапы, на которых решение было зачтено по тайм-ауту — от
    /// новых к старым</summary>
    public List<VndTimeoutApprovalItemResponse> Items { get; set; } = new();
}

/// <summary>Одна просрочка согласования — конкретный ВНД и фаза, на которой решение было
/// зачтено автоматически по истечении срока.</summary>
public class VndTimeoutApprovalItemResponse
{
    public int VndId { get; set; }
    public string VndCode { get; set; } = "";
    public string VndTitle { get; set; } = "";

    /// <summary>"primary" | "repeat" | "final" — та же фаза, что и в VndTaskResponse.StagePhase</summary>
    public string Phase { get; set; } = "";

    public DateTime DecidedAt { get; set; }
}
