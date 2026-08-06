using delosfera_server.Modules.Analytics.Services;

namespace delosfera_server.Extensions;

public static class AnalyticsServiceExtensions
{
    public static WebApplicationBuilder AddAnalyticsServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IVndAnalyticsService, VndAnalyticsService>();
        builder.Services.AddScoped<IUserAnalyticsService, UserAnalyticsService>();
        return builder;
    }
}
