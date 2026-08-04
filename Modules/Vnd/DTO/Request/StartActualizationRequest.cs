namespace delosfera_server.Modules.Vnd.DTO.Request;

/// <summary>Старт актуализации для пользователей с правом ActualizeAnyVnd(With/Without)Approval —
/// без запроса доступа, сразу.</summary>
public class StartActualizationRequest
{
    /// <summary>Ответственный за актуализацию. Если null — берём текущего пользователя.</summary>
    public int? ResponsibleUserId { get; set; }

    /// <summary>Сдвигать ли DueActualizationDate после публикации</summary>
    public required bool ShiftNextPeriod { get; set; }

    /// <summary>Актуализировать с согласованием или без.
    /// Без согласования доступно, только если у пользователя есть ActualizeAnyVndWithoutApproval.</summary>
    public required bool RequiresApproval { get; set; }
}