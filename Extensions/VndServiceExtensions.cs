using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Files.Services;

namespace delosfera_server.Extensions;

public static class VndServiceExtensions
{
    public static WebApplicationBuilder AddVndServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IVndService, VndService>();
        builder.Services.AddScoped<IFileAccessAuthorizer, VndFileAccessAuthorizer>();

        // Годовой план актуализации (PLN-01..07): реестр, импорт, связь с циклом
        // актуализации и напоминания о сроках
        builder.Services.AddScoped<IActualizationPlanService, ActualizationPlanService>();
        builder.Services.AddScoped<IActualizationPlanImportService, ActualizationPlanImportService>();
        builder.Services.AddScoped<IPlanItemLifecycleService, PlanItemLifecycleService>();
        builder.Services.AddScoped<IPlanItemSync, PlanItemSyncService>();
        builder.Services.AddScoped<IPlanReminderService, PlanReminderService>();
        builder.Services.AddHostedService<PlanReminderWorker>();

        // Пороги индикации сроков актуализации (Normal/Approaching/Critical) —
        // справочник в разделе ВНД, настраивается администратором
        builder.Services.AddScoped<IActualizationBucketSettingsService, ActualizationBucketSettingsService>();

        return builder;
    }
}