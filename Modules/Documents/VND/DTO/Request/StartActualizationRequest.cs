namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Старт актуализации для пользователей с правом ActualizeAnyVnd(With/Without)Approval —
/// без запроса доступа, сразу. Минимальный набор полей — только то, что нужно решить ДО перехода
/// в статус "На актуализации" (ответственный + порядок). Сдвиг срока и "актуализация без
/// изменений" решаются отдельно, позже, на шаге "Выполнить актуализацию" (см. PerformAsync) —
/// это осознанно вынесено из этого запроса.</summary>
public class StartActualizationRequest
{
    /// <summary>Ответственный за актуализацию. Если null — берём текущего пользователя.</summary>
    public int? ResponsibleUserId { get; set; }

    /// <summary>Актуализировать с согласованием или без.
    /// Без согласования доступно, только если у пользователя есть ActualizeAnyVndWithoutApproval.</summary>
    public required bool RequiresApproval { get; set; }
}
