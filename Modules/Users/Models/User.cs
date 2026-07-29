using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;

namespace delosfera_server.Modules.Users.Models;

/*
 Пользователи системы. 
*/

public class User : IAuditableEntity
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }

    public int? PositionId { get; set; }
    public Position? Position { get; set; }

    public int? OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Role> Roles { get; set; } = new List<Role>();
}