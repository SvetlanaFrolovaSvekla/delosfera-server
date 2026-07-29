namespace delosfera_server.Modules.Users.DTO.Response;

/// <summary>
/// Результат успешной аутентификации
/// </summary>
public class LoginResponse
{
    /// <summary>Access-токен (короткоживущий)</summary>
    public required string Token { get; set; }

    /// <summary>Refresh-токен (долгоживущий)</summary>
    public required string RefreshToken { get; set; }

    /// <summary>Данные аутентифицированного пользователя</summary>
    public required UserResponse User { get; set; }
}