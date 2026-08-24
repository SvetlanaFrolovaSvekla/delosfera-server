namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Запрос доступа к актуализации у главного редактора —
/// для пользователей с правом ActualizeVnd(With/Without)ApprovalByRequest.</summary>
public class RequestActualizationAccessRequest
{
    /// <summary>true — запрашивается право "с согласованием" (нужен ActualizeVndWithApprovalByRequest),
    /// false — "без согласования" (нужен ActualizeVndWithoutApprovalByRequest)</summary>
    public required bool RequiresApproval { get; set; }

    /// <summary>Личное пожелание заявителя насчёт сдвига DueActualizationDate после публикации.
    /// Главный редактор увидит это значение при рассмотрении заявки и сможет скорректировать его
    /// (см. ActualizationRequestDecisionRequest/DecideRequestAsync) — тогда в самой заявке
    /// останется уже финальное, а не изначально запрошенное значение.</summary>
    public required bool ShiftNextPeriod { get; set; }
}
