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
        //
        // ВРЕМЕННО ПАРАЛЛЕЛЬНО с InfocomEcpService ниже: в проекте сейчас две независимые
        // интеграции с одним и тем же провайдером (cdsapi-test.srs.kg) — эта (Oep, backend
        // напрямую) и SIG-ECP (backend → delosfera-ecp-service → провайдер). Обе оставлены
        // включаемыми по конфигу, чтобы не потерять код при слиянии веток — какую оставлять
        // в проекте, нужно решить отдельно с автором Oep и убрать вторую.
        builder.Services.Configure<OepOptions>(builder.Configuration.GetSection(OepOptions.Section));
        builder.Services.AddHttpClient<IOepSignatureClient, OepSignatureClient>();
        builder.Services.AddScoped<IOepSigningService, OepSigningService>();

        // ЭЦП ГП «Инфоком» (SIG-ECP, тестовый контур). Бекенд обращается не к провайдеру
        // напрямую, а к delosfera-ecp-service — своему сервису-прокладке (см.
        // InfocomEcpController / InfocomEcpClient). Адрес и токен — в appsettings:InfocomEcpService.
        builder.Services.Configure<InfocomEcpServiceOptions>(
            builder.Configuration.GetSection(InfocomEcpServiceOptions.Section));
        builder.Services.AddHttpClient<IInfocomEcpClient, InfocomEcpClient>()
            .ConfigurePrimaryHttpMessageHandler(sp => InfocomEcpClientHandler.Create(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InfocomEcpServiceOptions>>().Value,
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("InfocomEcpClient")));
        {
            // Предупреждаем при старте, а не только при первом запросе к ЭЦП.
            var ecpOptions = builder.Configuration.GetSection(InfocomEcpServiceOptions.Section).Get<InfocomEcpServiceOptions>()
                             ?? new InfocomEcpServiceOptions();
            if (Uri.TryCreate(ecpOptions.BaseUrl, UriKind.Absolute, out var ecpUri)
                && InfocomEcpClient.InsecureChannelReason(ecpUri, ecpOptions.AllowInsecureHttp) is { } reason)
                Console.Error.WriteLine($"[ЭЦП] {reason}. Запросы к ecp-service будут отклоняться.");
        }

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
