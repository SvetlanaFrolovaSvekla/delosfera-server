using delosfera_server.Modules.Dictionaries.DTO.Request;
using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Dictionaries.Services;

public interface IUserGroupService
{
    Task<List<UserGroupResponse>> GetAllAsync(UserGroupSortBy sortBy, string? search, string languageCode);
    Task<UserGroupResponse> CreateAsync(CreateUserGroupRequest request, string languageCode);
    Task<UserGroupResponse> UpdateAsync(int id, UpdateUserGroupRequest request, string languageCode);
    Task DeleteAsync(int id);
}