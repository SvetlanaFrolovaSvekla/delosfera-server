namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>
/// Данные для обновления токена
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>Refresh-токен, полученный при логине</summary>
    public required string RefreshToken { get; set; }
}