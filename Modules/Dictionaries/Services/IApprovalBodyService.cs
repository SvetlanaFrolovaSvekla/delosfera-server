using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface IApprovalBodyService
{
    Task<List<ApprovalBodyResponse>> GetAllAsync(ApprovalBodySortBy sortBy, string? search, string languageCode);
    Task<ApprovalBodyResponse> CreateAsync(CreateApprovalBodyRequest request, string languageCode);
    Task<ApprovalBodyResponse> UpdateAsync(int id, UpdateApprovalBodyRequest request, string languageCode);
    Task DeleteAsync(int id);
}