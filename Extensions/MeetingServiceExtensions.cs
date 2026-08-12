using delosfera_server.Modules.Meetings.Services;

namespace delosfera_server.Extensions;

public static class MeetingServiceExtensions
{
    /// <summary>
    /// Раздел «Заседания»: журнал заседаний Правления, КПА и комитетов, повестка,
    /// поручения по протоколам и напоминания об их исполнении.
    /// </summary>
    public static WebApplicationBuilder AddMeetingServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IMeetingAccessService, MeetingAccessService>();
        builder.Services.AddScoped<IMeetingService, MeetingService>();
        builder.Services.AddScoped<IAgendaService, AgendaService>();
        builder.Services.AddScoped<IAgendaFileService, AgendaFileService>();
        builder.Services.AddScoped<IMeetingNotificationService, MeetingNotificationService>();
        builder.Services.AddScoped<IMeetingRegistryService, MeetingRegistryService>();

        // Напоминания за 5 дней, в день срока и по просрочке — в 9:00 по времени банка.
        builder.Services.AddHostedService<MeetingReminderWorker>();

        return builder;
    }
}
