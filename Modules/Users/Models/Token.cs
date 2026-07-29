namespace delosfera_server.Modules.Users.Models;

public class Token
{
    public int Id { get; set; }
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public bool IsLoggedOut { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }
}