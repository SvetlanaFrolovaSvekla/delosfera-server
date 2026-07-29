using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;

namespace delosfera_server.Modules.Users.Services;

public interface IRoleService
{
    Task<List<PermissionResponse>> GetAllPermissionsAsync(string languageCode);
    Task<List<RoleResponse>> GetAllAsync(RoleSortBy sortBy, string? search, string languageCode);
    Task<RoleResponse> CreateAsync(CreateRoleRequest request, string languageCode);
    Task<RoleResponse> UpdateAsync(int id, UpdateRoleRequest request, string languageCode);
    Task DeleteAsync(int id);
}