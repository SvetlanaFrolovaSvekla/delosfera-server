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
        // Обработчик нужен и сам по себе: задачу адресату ставит он, а приводит
        // к ней не только маршрут — после регистрации без подписанта записка идёт
        // к адресату напрямую.
        builder.Services.AddScoped<SzRouteCompletionHandler>();
        builder.Services.AddScoped<IRouteCompletionHandler>(sp =>
            sp.GetRequiredService<SzRouteCompletionHandler>());
        return builder;
    }
}
