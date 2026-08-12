using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Extensions;

public static class SzServiceExtensions
{
    public static WebApplicationBuilder AddSzServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ISzService, SzService>();
        builder.Services.AddScoped<ISzExecutionService, SzExecutionService>();
        builder.Services.AddScoped<ISzPaperService, SzPaperService>();
        builder.Services.AddScoped<ISzArchiveService, SzArchiveService>();
        builder.Services.AddScoped<ISzProcurementService, SzProcurementService>();

        // Напоминания о сроках исполнения поручений в 9:00 по времени банка (SZ-03)
        builder.Services.AddScoped<ISzDeadlineNotifier, SzDeadlineNotifier>();

        // Статистика по запискам и её выгрузка (SZ-06)
        builder.Services.AddScoped<ISzStatisticsService, SzStatisticsService>();
        builder.Services.AddHostedService<SzDeadlineWorker>();
        builder.Services.AddScoped<IRouteCompletionHandler, SzRouteCompletionHandler>();
        return builder;
    }
}
