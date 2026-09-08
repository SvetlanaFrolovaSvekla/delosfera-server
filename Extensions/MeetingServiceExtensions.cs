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

        // Состав органов — отдельная настройка: банк меняет его решением, а не
        // перенастройкой прав.
        builder.Services.AddScoped<IBodyMemberService, BodyMemberService>();
        builder.Services.AddScoped<IMeetingService, MeetingService>();
        builder.Services.AddScoped<IAgendaService, AgendaService>();
        builder.Services.AddScoped<IAgendaFileService, AgendaFileService>();

        // Отбор записок с отметкой «вынести на орган» в повестку — решает секретарь
        builder.Services.AddScoped<IAgendaCandidateService, AgendaCandidateService>();
        builder.Services.AddScoped<IMeetingNotificationService, MeetingNotificationService>();
        builder.Services.AddScoped<IMeetingRegistryService, MeetingRegistryService>();

        // Напоминания за 5 дней, в день срока и по просрочке — в 9:00 по времени банка.
        builder.Services.AddHostedService<MeetingReminderWorker>();

        return builder;
    }
}
