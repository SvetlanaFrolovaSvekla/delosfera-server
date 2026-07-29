using delosfera_server.Modules.Dictionaries.DTO.Response;

namespace delosfera_server.Modules.Users.DTO.Response;

/// <summary>
/// Пользователь системы
/// </summary>
public class UserResponse
{
    public int Id { get; set; }

    /// <summary>ФИО пользователя</summary>
    public required string FullName { get; set; }

    /// <summary>Email — используется как логин</summary>
    public required string Email { get; set; }

    /// <summary>Должность (если назначена)</summary>
    public PositionResponse? Position { get; set; }

    /// <summary>Структурное подразделение (если назначено)</summary>
    public OrganizationUnitResponse? OrgUnit { get; set; }

    /// <summary>Активен ли пользователь</summary>
    public bool IsActive { get; set; }

    /// <summary>Дата последнего входа (если был)</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Роли пользователя</summary>
    public List<RoleResponse> Roles { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}