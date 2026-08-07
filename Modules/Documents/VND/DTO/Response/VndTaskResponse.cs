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

    // --- Только для coordination ---
    public int? RedactionId { get; set; }
    public string? RedactionCode { get; set; }
    public int? StageId { get; set; }
    /// <summary>"primary" | "repeat" — какой именно этап согласования сейчас ждёт решения</summary>
    public string? StagePhase { get; set; }
    public DateTime? DeadlineAt { get; set; }

    /// <summary>Инициатор редакции (кто отправил на согласование)</summary>
    public string? InitiatorName { get; set; }

    /// <summary>Норматив в минутах для текущей фазы согласования (Primary/Repeat)</summary>
    public int? DeadlineMinutes { get; set; }

    // --- Только для actualization/consolidation ---
    public DateOnly? DueActualizationDate { get; set; }

    public DateTime CreatedAt { get; set; }
}