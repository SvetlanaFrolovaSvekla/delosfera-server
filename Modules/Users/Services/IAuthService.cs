using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;

namespace delosfera_server.Modules.Users.Services;

/// <summary>
/// Результат аутентификации: тело ответа (access-токен + пользователь) и отдельно
/// сырой refresh-токен, который контроллер кладёт в httpOnly-cookie, а не в тело.
/// </summary>
public record AuthResult(LoginResponse Response, string RefreshToken);

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, string languageCode);

    /// <summary>Доменный вход: логин и пароль проверяются службой каталогов (INT-01).</summary>
    Task<AuthResult> LoginWithDirectoryAsync(DomainLoginRequest request, string languageCode);
    Task<AuthResult> RefreshAsync(string refreshToken, string languageCode);
    Task LogoutAsync(string refreshToken);
}
