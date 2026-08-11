using System.IdentityModel.Tokens.Jwt;
using delosfera_server.Common.Services;
using delosfera_server.Modules.Users.Models;

namespace Delosfera.Tests;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _jwt = new(TestSupport.Config());

    private static User User() => new()
    {
        Id = 42, FullName = "Иван Иванов", Email = "ivan@bank.kg", PasswordHash = "x"
    };

    [Fact]
    public void AccessToken_ContainsUserAndPermissionClaims()
    {
        var token = _jwt.GenerateAccessToken(User(), [11, 13]);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains(jwt.Claims, c => c.Type == "nameid" && c.Value == "42"
                                         || c.Type.EndsWith("nameidentifier") && c.Value == "42");
        var perms = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToArray();
        Assert.Contains("11", perms);
        Assert.Contains("13", perms);
    }

    [Fact]
    public void ExtractEmail_ReturnsSubject()
    {
        var token = _jwt.GenerateAccessToken(User(), []);
        Assert.Equal("ivan@bank.kg", _jwt.ExtractEmail(token));
    }

    [Fact]
    public void IsExpired_FreshToken_False()
    {
        var token = _jwt.GenerateAccessToken(User(), []);
        Assert.False(_jwt.IsExpired(token));
    }

    [Fact]
    public void IsExpired_Garbage_True()
    {
        Assert.True(_jwt.IsExpired("not-a-jwt"));
    }
}
