using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Users.Models;

/*
 Роли пользователей системы. Администратор может создавать новые роли,
 комбинируя фиксированный набор прав (PermissionCode).
*/

public class Role : IAuditableEntity, ITranslatableEntity
{
    public int Id { get; set; }
    public required string TitleRu { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleKg { get; set; }

    /// <summary>Коды прав, которыми обладает роль</summary>
    public int[] PermissionCodes { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}