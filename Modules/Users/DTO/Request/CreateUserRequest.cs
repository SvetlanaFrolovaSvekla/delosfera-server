using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>
/// Данные для создания нового пользователя
/// </summary>
public class CreateUserRequest
{
    /// <summary>ФИО пользователя (кириллицей)</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public required string FullName { get; set; }

    /// <summary>Email — используется как логин</summary>
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; set; }

    /// <summary>Пароль в открытом виде — будет захеширован на сервере</summary>
    [Required, StringLength(200, MinimumLength = 8)]
    public required string Password { get; set; }

    /// <summary>Идентификатор должности (если есть)</summary>
    public int? PositionId { get; set; }

    /// <summary>Идентификатор структурного подразделения (если есть)</summary>
    public int? OrgUnitId { get; set; }

    /// <summary>Коды ролей пользователя</summary>
    public List<int> RoleIds { get; set; } = [];
}
