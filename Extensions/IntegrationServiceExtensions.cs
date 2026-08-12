using delosfera_server.Modules.Integrations.Directory;
using delosfera_server.Modules.Integrations.Mail;

namespace delosfera_server.Extensions;

public static class IntegrationServiceExtensions
{
    /// <summary>Интеграции раздела 8 ТЗ: служба каталогов (INT-01), корпоративная почта (INT-02).</summary>
    public static WebApplicationBuilder AddIntegrationServices(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<LdapOptions>(builder.Configuration.GetSection(LdapOptions.Section));

        builder.Services.AddScoped<ILdapDirectory, LdapDirectory>();
        builder.Services.AddScoped<IDirectorySyncService, DirectorySyncService>();

        builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.Section));
        builder.Services.AddScoped<IMailQueue, MailQueue>();
        builder.Services.AddHostedService<MailWorker>();

        return builder;
    }
}
