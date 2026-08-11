namespace delosfera_server.Modules.Users.DTO.Response;

/// <summary>
/// Результат успешной аутентификации. Refresh-токен здесь НЕ возвращается —
/// он выставляется сервером в httpOnly-cookie и недоступен из JavaScript.
/// </summary>
public class LoginResponse
{
    /// <summary>Access-токен (короткоживущий). Клиент хранит его только в памяти.</summary>
    public required string Token { get; set; }

    /// <summary>Данные аутентифицированного пользователя</summary>
    public required UserResponse User { get; set; }
}
