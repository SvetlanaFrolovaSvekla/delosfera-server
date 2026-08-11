using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>
/// Данные для обновления пользователя
/// </summary>
public class UpdateUserRequest
{
    /// <summary>ФИО пользователя (кириллицей)</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public required string FullName { get; set; }

    /// <summary>Email — используется как логин</summary>
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; set; }

    /// <summary>Новый пароль — если не указан, пароль не меняется</summary>
    [StringLength(200, MinimumLength = 8)]
    public string? Password { get; set; }

    /// <summary>Идентификатор должности (если есть)</summary>
    public int? PositionId { get; set; }

    /// <summary>Идентификатор структурного подразделения (если есть)</summary>
    public int? OrgUnitId { get; set; }

    /// <summary>Активен ли пользователь</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Коды ролей пользователя</summary>
    public List<int> RoleIds { get; set; } = [];
}