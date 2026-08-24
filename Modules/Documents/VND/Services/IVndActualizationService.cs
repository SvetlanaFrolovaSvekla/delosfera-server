using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndActualizationService
{
    /// <summary>Шаг А: сразу начать актуализацию — для ActualizeAnyVndWithApproval/WithoutApproval.
    /// Только переводит документ в "На актуализации" и фиксирует ответственного/порядок —
    /// сдвиг срока и "без изменений" решаются отдельно, см. PerformAsync</summary>
    Task<VndActualizationStateResponse> StartAsync(int vndId, StartActualizationRequest request, int currentUserId);

    /// <summary>Шаг Б (для цикла, начатого через StartAsync): выполнить актуализацию — зафиксировать
    /// финальные сдвиг срока/"без изменений". До этого шага загрузка новой редакции заблокирована</summary>
    Task<VndActualizationStateResponse> PerformAsync(
        int vndId, PerformActualizationRequest request, int currentUserId);

    /// <summary>Запросить доступ к актуализации — для ActualizeVnd...ByRequest</summary>
    Task<VndActualizationRequestResponse> RequestAccessAsync(
        int vndId, RequestActualizationAccessRequest request, int currentUserId);

    /// <summary>Список заявок в статусе Pending — для главного редактора</summary>
    Task<List<VndActualizationRequestResponse>> GetPendingRequestsAsync(int currentUserId);

    /// <summary>Решение по заявке — approve/reject, с возможной корректировкой сдвига срока
    /// и автоотклонением остальных pending-заявок по тому же ВНД при одобрении</summary>
    Task<VndActualizationRequestResponse> DecideRequestAsync(
        int requestId, ActualizationRequestDecisionRequest request, int currentUserId);

    /// <summary>Подтвердить старт актуализации после одобренной заявки — совмещает старт цикла
    /// и шаг "Выполнить актуализацию" (единственная кнопка для пути "по заявке")</summary>
    Task<VndActualizationStateResponse> ConfirmStartAfterRequestAsync(
        int vndId, ConfirmActualizationStartRequest request, int currentUserId);

    /// <summary>Подтвердить, что заявленная "актуализация без изменений" прошла без изменений и
    /// без согласования — OnActualization → Consolidation напрямую, без загрузки новой редакции</summary>
    Task<VndActualizationStateResponse> ConfirmNoChangesAsync(int vndId, int currentUserId);

    /// <summary>Опубликовать новую редакцию — Consolidation → Active</summary>
    Task<VndActualizationStateResponse> PublishAsync(
        int vndId, PublishVndActualizationRequest request, int currentUserId);

    /// <summary>История циклов актуализации документа — кто и когда актуализировал,
    /// от самого нового к самому старому</summary>
    Task<List<VndActualizationRecordResponse>> GetHistoryAsync(int vndId);

    /// <summary>Все заявки на доступ к актуализации этого документа — любого статуса
    /// (Pending/Approved/Rejected), от самой новой к самой старой. В отличие от
    /// GetPendingRequestsAsync — не ограничено главным редактором, доступно всем,
    /// кто может просматривать сам ВНД (нужно для истории/аудита и для того, чтобы
    /// заявитель мог узнать статус своей заявки по конкретному документу).</summary>
    Task<List<VndActualizationRequestResponse>> GetRequestHistoryAsync(int vndId);
}