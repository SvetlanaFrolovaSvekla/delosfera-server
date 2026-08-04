namespace delosfera_server.Modules.Vnd.DTO.Request;

/// <summary>Подтверждение старта актуализации после одобренной заявки на доступ.
/// RequiresApproval здесь не спрашиваем — он уже зафиксирован в самой заявке.</summary>
public class ConfirmActualizationStartRequest
{
    public required bool ShiftNextPeriod { get; set; }
}