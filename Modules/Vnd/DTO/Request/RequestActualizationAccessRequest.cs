namespace delosfera_server.Modules.Vnd.DTO.Request;

/// <summary>Запрос доступа к актуализации у главного редактора —
/// для пользователей с правом ActualizeVnd(With/Without)ApprovalByRequest.</summary>
public class RequestActualizationAccessRequest
{
    /// <summary>true — запрашивается право "с согласованием" (нужен ActualizeVndWithApprovalByRequest),
    /// false — "без согласования" (нужен ActualizeVndWithoutApprovalByRequest)</summary>
    public required bool RequiresApproval { get; set; }
}