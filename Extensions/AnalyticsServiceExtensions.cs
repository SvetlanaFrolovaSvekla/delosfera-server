using delosfera_server.Modules.Analytics.Services;

namespace delosfera_server.Extensions;

public static class AnalyticsServiceExtensions
{
    public static WebApplicationBuilder AddAnalyticsServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IVndAnalyticsService, VndAnalyticsService>();

        // Выгрузка аналитических срезов в Excel (RPT-03)
        builder.Services.AddScoped<IVndAnalyticsExportService, VndAnalyticsExportService>();
        builder.Services.AddScoped<IUserAnalyticsService, UserAnalyticsService>();
        builder.Services.AddScoped<IDashboardService, DashboardService>();
        return builder;
    }
}
