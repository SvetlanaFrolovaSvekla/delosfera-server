using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;

namespace delosfera_server.Modules.Vnd.Services;

public interface IVndService
{
    Task<List<VndResponse>> SearchAsync(VndSearchRequest request, string languageCode);
    Task<VndResponse> GetByIdAsync(int id, string languageCode);
    Task<VndResponse> CreateAsync(CreateVndRequest request, int currentUserId, string languageCode);
    Task<VndRedactionResponse> AddRedactionAsync(int vndId, CreateVndRedactionRequest request, int currentUserId);
    Task<List<VndRedactionResponse>> GetRedactionsAsync(int vndId);
    Task<VndRedactionResponse> SubmitRedactionForApprovalAsync(int vndId, int redactionId);
    Task<VndActualizationSummaryResponse> GetActualizationSummaryAsync();
    Task<VndResponse> UpdateRequisitesAsync(int id, UpdateVndRequisitesRequest request, string languageCode);

}