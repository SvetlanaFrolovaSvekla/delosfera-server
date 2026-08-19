using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using delosfera_server.Data;

namespace delosfera_server.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<DelosferaDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        return builder;
    }
}