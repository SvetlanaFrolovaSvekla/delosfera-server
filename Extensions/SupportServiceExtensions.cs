using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Signing.Services;

namespace delosfera_server.Extensions;

public static class SupportServiceExtensions
{
    public static WebApplicationBuilder AddSigningServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ISignatureService, SignatureService>();
        builder.Services.AddScoped<IDocumentFingerprintService, DocumentFingerprintService>();
        builder.Services.AddScoped<ISimpleSignatureRegulationService, SimpleSignatureRegulationService>();

        // КЭП: проверка подписи хеша версии и реквизитов сертификата (SIG-02, INT-03)
        builder.Services.AddScoped<IQualifiedSignatureService, QualifiedSignatureService>();
        builder.Services.AddScoped<ICertificateTrustService, CertificateTrustService>();

        // Метки времени и проверка отзыва (Б-18). Отдельный клиент, чтобы таймаут
        // службы меток не влиял на остальные обращения наружу.
        builder.Services.AddHttpClient("timestamp");
        builder.Services.AddScoped<ITimestampService, TimestampService>();
        return builder;
    }

    public static WebApplicationBuilder AddNotificationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<INotificationService, NotificationService>();
        return builder;
    }
}
