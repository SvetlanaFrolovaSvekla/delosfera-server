namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndTaskResponse
{
    public int VndId { get; set; }
    public required string VndCode { get; set; }
    public required string VndTitle { get; set; }

    /// <summary>"coordination" | "actualization" | "consolidation" | "myVndApproval"</summary>
    public required string Scope { get; set; }

    /// <summary>Человекочитаемый статус процесса (например, "В процессе согласования первой редакции ВНД").
    /// Заполняется для myVndApproval и consolidation, где важно показать, на каком именно этапе сейчас ВНД.</summary>
    public string? StatusLabel { get; set; }

    /// <summary>Текущий статус самого ВНД: "active" | "onact" | "review" | "consol" | "arch" | "draft" —
    /// показывается на карточке задачи для дополнительного контекста.</summary>
    public string? VndStatus { get; set; }

    // --- Только для coordination ---
    public int? RedactionId { get; set; }
    public string? RedactionCode { get; set; }
    public int? StageId { get; set; }
    /// <summary>"primary" | "repeat" | "final" — текущая фаза согласования. Заполняется для
    /// coordination (какой этап ждёт решения текущего пользователя) и для myVndApproval
    /// (на каком круге сейчас редакция инициатора) — используется для фильтра "Этап
    /// согласования" на странице "Мои задачи".</summary>
    public string? StagePhase { get; set; }
    /// <summary>Профиль этапа: "legal" | "risk_management" | "compliance" | "custom" | "methodology" —
    /// подразделение, отвечающее за этап (только для coordination).</summary>
    public string? StageKind { get; set; }
    public DateTime? DeadlineAt { get; set; }

    /// <summary>Инициатор редакции (кто отправил на согласование)</summary>
    public string? InitiatorName { get; set; }

    /// <summary>Норматив в минутах для текущей фазы согласования (Primary/Repeat/FinalHold)</summary>
    public int? DeadlineMinutes { get; set; }

    /// <summary>Комментарий инициатора к повторному кругу/финальной выдержке — контекст,
    /// зачем документ снова пришёл на согласование.</summary>
    public string? InitiatorComment { get; set; }

    // --- Только для actualization/consolidation ---
    public DateOnly? DueActualizationDate { get; set; }

    public DateTime CreatedAt { get; set; }
}
