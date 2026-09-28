namespace delosfera_server.Modules.Signing.Services.Oep;

/// <summary>
/// Клиент API облачной ЭП ГП «Инфоком» (спецификация v2.3). Тонкая обёртка над
/// шестью методами сервиса; бизнес-оркестрацию (последовательность шагов, привязку
/// к документу и сохранение подписи) держит <see cref="IOepSigningService"/>.
///
/// Все методы кидают <see cref="OepApiException"/> с кодом и текстом из ответа
/// Инфокома при ошибке, и <see cref="InvalidOperationException"/> если интеграция
/// не настроена.
/// </summary>
public interface IOepSignatureClient
{
    bool Enabled { get; }

    /// <summary>2.1 Доступные методы авторизации (email/sms) и статус учётной записи.</summary>
    Task<OepAuthMethodsResponse> GetAuthMethodsAsync(
        string? personIdnp, string? organizationInn, CancellationToken ct = default);

    /// <summary>2.2 Отправить пин-код выбранным методом.</summary>
    Task<OepSendPinResponse> SendPinAsync(
        string? personIdnp, string? organizationInn, string method, CancellationToken ct = default);

    /// <summary>2.3 Получить userToken по пин-коду (действует ~1 минуту).</summary>
    Task<OepAuthResponse> AuthenticateAsync(
        string? personIdnp, string? organizationInn, string byPin, CancellationToken ct = default);

    /// <summary>2.4 Сведения о сертификате по userToken.</summary>
    Task<OepCertInfoResponse> GetCertInfoAsync(string userToken, CancellationToken ct = default);

    /// <summary>2.5 Подписать хэш — вернёт CMS-подпись (base64).</summary>
    Task<OepSignHashResponse> SignHashAsync(string hashHex, string userToken, CancellationToken ct = default);

    /// <summary>2.6 Проверить подпись по хэшу на стороне Инфокома.</summary>
    Task<OepCheckSignResponse> CheckSignAsync(
        string signBase64, string hashHex, string? personIdnp, CancellationToken ct = default);
}
