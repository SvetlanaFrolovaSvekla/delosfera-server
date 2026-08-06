using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Users.Services;

public interface IUserService
{
    Task<List<UserResponse>> GetAllAsync(
        UserSortBy sortBy,
        string? search,
        List<int>? orgUnitIds,
        List<int>? positionIds,
        List<int>? roleIds,
        UserSource? source,
        bool? isBlocked,
        string languageCode);

    Task<UserResponse> GetByIdAsync(int id, string languageCode);
    Task<UserResponse> CreateAsync(CreateUserRequest request, string languageCode);
    Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, string languageCode);
    Task DeleteAsync(int id);

    Task<UserResponse> BlockAsync(int id, int blockedByUserId, string? reason, string languageCode);
    Task<UserResponse> UnblockAsync(int id, string languageCode);
}