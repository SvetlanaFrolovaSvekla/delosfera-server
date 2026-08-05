using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;

namespace delosfera_server.Modules.Vnd.Services;

public interface IVndActualizationService
{
    /// <summary>Сразу начать актуализацию — для ActualizeAnyVndWithApproval/WithoutApproval</summary>
    Task<VndActualizationStateResponse> StartAsync(int vndId, StartActualizationRequest request, int currentUserId);

    /// <summary>Запросить доступ к актуализации — для ActualizeVnd...ByRequest</summary>
    Task<VndActualizationRequestResponse> RequestAccessAsync(
        int vndId, RequestActualizationAccessRequest request, int currentUserId);

    /// <summary>Список заявок в статусе Pending — для главного редактора</summary>
    Task<List<VndActualizationRequestResponse>> GetPendingRequestsAsync(int currentUserId);

    /// <summary>Решение по заявке — approve/reject</summary>
    Task<VndActualizationRequestResponse> DecideRequestAsync(int requestId, bool approve, int currentUserId);

    /// <summary>Подтвердить старт актуализации после одобренной заявки (только сдвиг периода)</summary>
    Task<VndActualizationStateResponse> ConfirmStartAfterRequestAsync(
        int vndId, ConfirmActualizationStartRequest request, int currentUserId);

    /// <summary>Опубликовать новую редакцию — Consolidation → Active</summary>
    Task<VndActualizationStateResponse> PublishAsync(
        int vndId, PublishVndActualizationRequest request, int currentUserId);

    /// <summary>История циклов актуализации документа — кто и когда актуализировал,
    /// от самого нового к самому старому</summary>
    Task<List<VndActualizationRecordResponse>> GetHistoryAsync(int vndId);
}