using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndApprovalService
{
    Task<ApprovalProcessResponse> StartAsync(int vndId, StartApprovalRequest request, int currentUserId);
    Task<ApprovalProcessResponse> GetByVndIdAsync(int vndId);
    /// <summary>История ВСЕХ процессов согласования этого ВНД — по всем редакциям и циклам
    /// актуализации, включая уже завершённые/отозванные/отклонённые. Для таба "История" ->
    /// "Редакции и юридическая значимость" (кто инициировал согласование каждой редакции,
    /// кто и как согласовывал).</summary>
    Task<List<ApprovalProcessResponse>> GetHistoryByVndIdAsync(int vndId);
    Task<ApprovalProcessResponse> DecideAsync(int vndId, int stageId, ApprovalDecisionRequest request, int currentUserId);
    Task<ApprovalProcessResponse> CancelAsync(int vndId, int currentUserId);
    /// <summary>Отзыв согласования как часть архивации ВНД (см. VndService.CancelAsync) —
    /// та же логика, что и у CancelAsync выше, но без проверки "инициатор или
    /// CancelAnyVndApproval": архивирующего уже авторизовало отдельное право CancelVnd на саму
    /// архивацию, требовать от него ещё и права отзывать чужое согласование не нужно.</summary>
    Task WithdrawForCancelAsync(int vndId, int currentUserId);
    Task<ApprovalProcessResponse> ResubmitAfterRevisionAsync(int vndId, ResubmitAfterRevisionRequest request, int currentUserId);
    Task<DisagreementMatrixRowResponse> AddDisagreementMatrixRowAsync(int vndId, AddDisagreementMatrixRowRequest request, int currentUserId);
    Task<DisagreementMatrixRowResponse> UpdateDisagreementMatrixRowAsync(int vndId, int rowId, UpdateDisagreementMatrixRowRequest request, int currentUserId);
    Task DeleteDisagreementMatrixRowAsync(int vndId, int rowId, int currentUserId);

    /// <summary>Главный редактор добавляет согласующего в уже запущенный процесс согласования —
    /// маршрут редактируется на лету, без остановки согласования. Доступно только с правом
    /// EditAnyVndApprovalRoute. См. VndApprovalStage.IsRemovedByEditor — новый этап встраивается
    /// в ту фазу, которая сейчас активна.</summary>
    Task<ApprovalProcessResponse> AddApproverAsync(int vndId, AddApprovalStageRequest request, int currentUserId);

    /// <summary>Главный редактор убирает согласующего из уже запущенного процесса согласования —
    /// этап не удаляется (история согласования не теряется), а помечается недействующим
    /// (VndApprovalStage.IsRemovedByEditor), задача с него снимается. Доступно только с правом
    /// EditAnyVndApprovalRoute.</summary>
    Task<ApprovalProcessResponse> RemoveApproverAsync(int vndId, int stageId, RemoveApprovalStageRequest request, int currentUserId);

    Task ProcessTimeoutsAsync();
}