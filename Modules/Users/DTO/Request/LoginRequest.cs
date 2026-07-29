namespace delosfera_server.Modules.Users.DTO.Request;

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}