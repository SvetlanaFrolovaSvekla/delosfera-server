namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>
/// Данные для блокировки пользователя
/// </summary>
public class BlockUserRequest
{
    /// <summary>Причина блокировки (опционально)</summary>
    public string? Reason { get; set; }
}