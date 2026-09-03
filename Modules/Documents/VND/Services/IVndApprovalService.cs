using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndApprovalService
{
    Task<ApprovalProcessResponse> StartAsync(int vndId, StartApprovalRequest request, int currentUserId);
    Task<ApprovalProcessResponse> GetByVndIdAsync(int vndId);
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
    Task ProcessTimeoutsAsync();
}