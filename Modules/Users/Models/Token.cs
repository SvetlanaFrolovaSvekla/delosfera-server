namespace delosfera_server.Modules.Users.Models;

/// <summary>
/// Серверная запись refresh-сессии. Сам refresh-токен не хранится — только его SHA-256
/// хеш, поэтому утечка БД не даёт готовых токенов. Access-токен не хранится вовсе
/// (он stateless и на сервере не проверяется по БД).
/// </summary>
public class Token
{
    public int Id { get; set; }

    /// <summary>SHA-256 (hex) от выданного refresh-токена.</summary>
    public required string RefreshTokenHash { get; set; }

    /// <summary>Момент истечения refresh-токена (UTC). Проверяется вместо разбора JWT.</summary>
    public DateTime ExpiresAt { get; set; }

    public bool IsLoggedOut { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }
}
