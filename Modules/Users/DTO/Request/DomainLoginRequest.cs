using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Users.DTO.Request;

/// <summary>
/// Доменный вход (INT-01). Логин — доменный, а не адрес почты: сотрудник вводит то же,
/// чем входит в рабочую станцию.
/// </summary>
public class DomainLoginRequest
{
    [Required, StringLength(256, MinimumLength = 1)]
    public required string Login { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public required string Password { get; set; }
}
