using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;

namespace delosfera_server.Modules.Users.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string languageCode);
    Task<LoginResponse> RefreshAsync(RefreshTokenRequest request, string languageCode);
    Task LogoutAsync(string refreshToken);
}