using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>
/// Данные для обновления роли
/// </summary>
public class UpdateRoleRequest
{
    /// <summary>Название на русском (обязательно)</summary>
    [Required, StringLength(300, MinimumLength = 1)]
    public required string TitleRu { get; set; }

    /// <summary>Название на английском (опционально)</summary>
    [StringLength(300)]
    public string? TitleEn { get; set; }

    /// <summary>Название на киргизском (опционально)</summary>
    [StringLength(300)]
    public string? TitleKg { get; set; }

    /// <summary>Коды прав, которыми будет обладать роль</summary>
    public List<int> PermissionCodes { get; set; } = [];
}