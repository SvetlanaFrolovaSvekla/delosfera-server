using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Common.Services;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, List<int> permissionCodes);
    string GenerateRefreshToken(User user);
    string? ExtractEmail(string token);
    bool IsExpired(string token);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private SymmetricSecurityKey GetSigningKey() =>
        new(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

    public string GenerateAccessToken(User user, List<int> permissionCodes)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName)
        };
        claims.AddRange(permissionCodes.Select(code => new Claim("permission", code.ToString())));

        var expiryMinutes = int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"] ?? "30");
        return BuildToken(claims, TimeSpan.FromMinutes(expiryMinutes));
    }

    public string GenerateRefreshToken(User user)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Email) };
        var expiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "30");
        return BuildToken(claims, TimeSpan.FromDays(expiryDays));
    }

    private string BuildToken(List<Claim> claims, TimeSpan lifetime)
    {
        var credentials = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string? ExtractEmail(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public bool IsExpired(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}