using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Signing.Services.Oep;

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

        // Облачная ЭП ГП «Инфоком» (ОЭП, INT-03): подпись отпечатка через облако УЦ
        // по пин-коду. Типизированный HttpClient — свой таймаут, Bearer и User-Agent
        // ставятся в самом клиенте. Без Oep:BaseUrl/ServiceToken интеграция выключена.
        builder.Services.Configure<OepOptions>(builder.Configuration.GetSection(OepOptions.Section));
        builder.Services.AddHttpClient<IOepSignatureClient, OepSignatureClient>();
        builder.Services.AddScoped<IOepSigningService, OepSigningService>();

        // Ознакомление с документами (Б-19): роспись сотрудника под приказом или ВНД
        builder.Services.AddScoped<IAcknowledgementService, AcknowledgementService>();

        // Очистка разметки текста документов: редактор отдаёт HTML, а он попадёт
        // в браузер другого сотрудника (SZ-02).
        builder.Services.AddSingleton<Common.Services.IDocumentHtmlService,
            Common.Services.DocumentHtmlService>();
        return builder;
    }

    public static WebApplicationBuilder AddNotificationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<INotificationSettingService, NotificationSettingService>();
        return builder;
    }
}
