using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;

namespace delosfera_server.Modules.Vnd.Services;

public interface IVndApprovalService
{
    Task<ApprovalProcessResponse> StartAsync(int vndId, StartApprovalRequest request, int currentUserId);
    Task<ApprovalProcessResponse> GetByVndIdAsync(int vndId);
    Task<ApprovalProcessResponse> DecideAsync(int vndId, int stageId, ApprovalDecisionRequest request, int currentUserId);
    Task<ApprovalProcessResponse> ResubmitAfterRevisionAsync(int vndId, ResubmitAfterRevisionRequest request, int currentUserId);
    Task<DisagreementMatrixRowResponse> AddDisagreementMatrixRowAsync(int vndId, AddDisagreementMatrixRowRequest request, int currentUserId);
    Task DeleteDisagreementMatrixRowAsync(int vndId, int rowId, int currentUserId);
    Task ProcessTimeoutsAsync();
}