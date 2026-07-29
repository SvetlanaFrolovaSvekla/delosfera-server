namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>
/// Данные для создания новой роли
/// </summary>
public class CreateRoleRequest
{
    /// <summary>Название на русском (обязательно)</summary>
    public required string TitleRu { get; set; }

    /// <summary>Название на английском (опционально)</summary>
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском (опционально)</summary>
    public string? TitleKg { get; set; }

    /// <summary>Коды прав, которыми будет обладать роль</summary>
    public List<int> PermissionCodes { get; set; } = [];
}