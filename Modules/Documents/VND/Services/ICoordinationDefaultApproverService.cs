using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface ICoordinationDefaultApproverService
{
    Task<List<CoordinationDefaultApproverResponse>> GetAllAsync();
    Task<CoordinationDefaultApproverResponse> UpdateAsync(int id, UpdateCoordinationDefaultApproverRequest request);
}