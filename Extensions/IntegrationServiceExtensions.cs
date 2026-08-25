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

        // Настройки связи со службой каталогов хранятся в базе и правятся администратором
        // через интерфейс; конфигурация сервера служит лишь первоначальным значением.
        builder.Services.AddSingleton<delosfera_server.Common.Security.ISecretProtector,
            delosfera_server.Common.Security.SecretProtector>();
        builder.Services.AddScoped<IDirectorySettingsService, DirectorySettingsService>();

        builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.Section));
        builder.Services.AddScoped<IMailQueue, MailQueue>();
        builder.Services.AddHostedService<MailWorker>();

        // Оргструктура приходит из портала банка. Обращение идёт по сети к чужой
        // системе, поэтому у клиента свой срок ожидания: обход сотрудников
        // страницами дольше обычного запроса, а висеть бесконечно он не должен.
        builder.Services.AddHttpClient<delosfera_server.Modules.Integrations.OrgStructure.PortalOrgClient>(
            client => client.Timeout = TimeSpan.FromSeconds(120));

        builder.Services.AddScoped<
            delosfera_server.Modules.Integrations.OrgStructure.IOrgSyncService,
            delosfera_server.Modules.Integrations.OrgStructure.OrgSyncService>();

        builder.Services.AddHostedService<
            delosfera_server.Modules.Integrations.OrgStructure.OrgSyncWorker>();

        return builder;
    }
}
