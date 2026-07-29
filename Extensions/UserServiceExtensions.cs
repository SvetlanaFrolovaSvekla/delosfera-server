using delosfera_server.Common.Services;
using delosfera_server.Modules.Users.Services;

namespace delosfera_server.Extensions;

public static class UserServiceExtensions
{
    public static WebApplicationBuilder AddUserServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IRoleService, RoleService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IUserPasswordHasher, UserPasswordHasher>();
        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        return builder;
    }
}