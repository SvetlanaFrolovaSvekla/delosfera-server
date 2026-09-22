namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Запрос доступа к актуализации у главного редактора — для пользователей с правом
/// ActualizeVnd(With/Without)ApprovalByRequest (любым из двух — см. RequestAccessAsync).
///
/// Пустое тело: заявка всегда уходит "с последующим согласованием" (без согласования может
/// начать актуализацию только главный редактор напрямую — см. StartActualizationRequest), и
/// сдвиг срока следующей актуализации заявитель больше не выбирает — это решает исключительно
/// главный редактор при одобрении (см. ActualizationRequestDecisionRequest).</summary>
public class RequestActualizationAccessRequest
{
}
