namespace delosfera_server.Modules.Users.DTO.Response;

/// <summary>
/// Описание одного права доступа
/// </summary>
public class PermissionResponse
{
    /// <summary>Числовой код права</summary>
    public int Code { get; set; }

    /// <summary>Системное имя права (для отладки/логов)</summary>
    public required string Key { get; set; }

    /// <summary>Человекочитаемое описание права (на русском)</summary>
    public required string Description { get; set; }
}