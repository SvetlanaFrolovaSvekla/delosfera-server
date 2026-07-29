namespace delosfera_server.Modules.Users.DTO.Response;

/// <summary>
/// Роль из справочника
/// </summary>
public class RoleResponse
{
    /// <summary>Уникальный идентификатор</summary>
    public int Id { get; set; }

    /// <summary>Название, разрешённое под язык текущего запроса</summary>
    public required string Name { get; set; }

    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Коды прав роли (для отправки на бэк при редактировании)</summary>
    public List<int> PermissionCodes { get; set; } = [];

    /// <summary>Расшифрованные права роли — код + описание (для отображения)</summary>
    public List<PermissionResponse> Permissions { get; set; } = [];

    /// <summary>Дата создания записи</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата последнего обновления записи</summary>
    public DateTime UpdatedAt { get; set; }
}