using System.ComponentModel.DataAnnotations;

namespace delosfera_server.Modules.Users.DTO.Request;

public class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public required string Password { get; set; }
}
