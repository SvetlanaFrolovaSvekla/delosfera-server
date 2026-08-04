using delosfera_server.Modules.Vnd.DTO.Request;
using delosfera_server.Modules.Vnd.DTO.Response;

namespace delosfera_server.Modules.Vnd.Services;

public interface ICoordinationDefaultApproverService
{
    Task<List<CoordinationDefaultApproverResponse>> GetAllAsync();
    Task<CoordinationDefaultApproverResponse> UpdateAsync(int id, UpdateCoordinationDefaultApproverRequest request);
}