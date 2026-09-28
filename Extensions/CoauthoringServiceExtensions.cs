using delosfera_server.Modules.Coauthoring.Options;
using delosfera_server.Modules.Coauthoring.Services;

namespace delosfera_server.Extensions;

public static class CoauthoringServiceExtensions
{
    /// <summary>
    /// Онлайн-соредактирование вложений через OnlyOffice Document Server (COOP-1).
    /// Секреты секции OnlyOffice берутся из конфигурации (env / user-secrets), сам
    /// Document Server — внешняя инфраструктура (см. docker-compose.onlyoffice.yml).
    /// </summary>
    public static WebApplicationBuilder AddCoauthoringServices(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<OnlyOfficeOptions>(
            builder.Configuration.GetSection(OnlyOfficeOptions.Section));

        builder.Services.AddScoped<ICoauthoringService, CoauthoringService>();
        builder.Services.AddScoped<IAttachmentVersionStorage, MinioAttachmentVersionStorage>();
        builder.Services.AddScoped<IOnlyOfficeDocumentFetcher, HttpOnlyOfficeDocumentFetcher>();
        builder.Services.AddHttpClient(nameof(HttpOnlyOfficeDocumentFetcher));

        return builder;
    }
}
