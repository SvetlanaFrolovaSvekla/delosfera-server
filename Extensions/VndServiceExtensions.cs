using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Files.Services;

namespace delosfera_server.Extensions;

public static class VndServiceExtensions
{
    public static WebApplicationBuilder AddVndServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IVndService, VndService>();

        // Легаси-гиперссылки db://documents/{код} (isrib) из текста редакций - индекс в vnd_link,
        // чтобы видеть, какая редакция ссылается, и показывать "Ссылающиеся документы" у цели.
        builder.Services.AddScoped<IVndLegacyLinkIndexer, VndLegacyLinkIndexer>();
        builder.Services.AddHostedService<VndLegacyLinkIndexWorker>();
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
        // справочник в разделе ВНД, настраивается администратором. Воркер держит
        // статический in-memory кэш порогов (ActualizationThresholds) актуальным после
        // рестарта процесса и на репликах, где справочник ни разу не открывали — см.
        // ActualizationThresholdsRefreshWorker.
        builder.Services.AddScoped<IActualizationBucketSettingsService, ActualizationBucketSettingsService>();
        builder.Services.AddHostedService<ActualizationThresholdsRefreshWorker>();

        // Нормативы согласования редакции ВНД по умолчанию (первичное / после изменений /
        // финальная выдержка) — справочник в разделе ВНД, предзаполняет модалку запуска
        // согласования.
        builder.Services.AddScoped<IVndApprovalNormSettingsService, VndApprovalNormSettingsService>();

        // Производственный календарь ВНД (праздники / переносы / сокращённые дни) — справочник
        // в разделе ВНД. Сроки согласования редакций считаются только в рабочее время
        // (пн–пт 09:00–18:00 по Бишкеку без праздников). Кэш — singleton: справочник крошечный
        // и нужен на каждом старте фазы согласования.
        builder.Services.AddSingleton<IVndWorkCalendarCache, VndWorkCalendarCache>();
        builder.Services.AddScoped<IVndWorkCalendarService, VndWorkCalendarService>();
        builder.Services.AddScoped<IVndFavoriteService, VndFavoriteService>();

        // Раздел "Уведомления" → "Настройки рассылок" → "Нормотворчество": ответственные
        // сотрудники СП за актуализацию и ежемесячная сводка им 1-го числа
        builder.Services.AddScoped<IActualizationNotificationService, ActualizationNotificationService>();
        builder.Services.AddHostedService<ActualizationNotificationWorker>();

        // Предложения по ВНД от сотрудников главному редактору ("+ Предложения по ВНД" на
        // странице документа и страница "Предложения по ВНД" в разделе "Нормотворчество")
        builder.Services.AddScoped<IVndProposalService, VndProposalService>();

        return builder;
    }
}