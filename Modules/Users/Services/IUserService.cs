using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;

namespace delosfera_server.Modules.Users.Services;

public interface IUserService
{
    Task<List<UserResponse>> GetAllAsync(UserSortBy sortBy, string? search, string languageCode);
    Task<UserResponse> GetByIdAsync(int id, string languageCode); // ⬅️ новое
    Task<UserResponse> CreateAsync(CreateUserRequest request, string languageCode);
    Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, string languageCode);
    Task DeleteAsync(int id);
}