using delosfera_server.Modules.Integrations.Directory;

namespace delosfera_server.Extensions;

public static class IntegrationServiceExtensions
{
    /// <summary>Интеграции раздела 8 ТЗ: служба каталогов AD/LDAP (INT-01).</summary>
    public static WebApplicationBuilder AddIntegrationServices(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<LdapOptions>(builder.Configuration.GetSection(LdapOptions.Section));

        builder.Services.AddScoped<ILdapDirectory, LdapDirectory>();
        builder.Services.AddScoped<IDirectorySyncService, DirectorySyncService>();

        return builder;
    }
}
