namespace delosfera_server.Modules.Signing.Services.Oep;

/// <summary>
/// Настройки облачной ЭП ГП «Инфоком» (API ОЭП, раздел 2 спецификации v2.3).
///
/// ServiceToken — токен доступа организации к инфраструктуре облачных подписей
/// (Bearer на КАЖДОМ запросе). Хранить конфиденциально, задавать через env/secrets,
/// не в appsettings.json. Пустой BaseUrl или ServiceToken = интеграция выключена
/// (Enabled == false), контур подписания работает без облачной ЭП.
/// </summary>
public sealed class OepOptions
{
    public const string Section = "Oep";

    /// <summary>База API, напр. https://cdsapi-test.srs.kg/api (тест) или боевой адрес.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Токен доступа организации (Bearer). Секрет — только env/secrets.</summary>
    public string ServiceToken { get; set; } = "";

    /// <summary>
    /// Наименование организации без спец-символов — уходит в User-Agent (требование
    /// п.6 ограничений: по нему Инфоком опознаёт пользователя токена).
    /// </summary>
    public string OrganizationName { get; set; } = "";

    /// <summary>
    /// ИНН организации по умолчанию для представителей юр. лица. Может быть
    /// переопределён в запросе; для подписи физ. лица не обязателен.
    /// </summary>
    public string? OrganizationInn { get; set; }

    /// <summary>Таймаут обращения к API ОЭП, секунды.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Проверять полученную подпись обратным вызовом check-sign перед сохранением.
    /// Ошибка проверки → подпись не принимается. По умолчанию включено.
    /// </summary>
    public bool VerifyAfterSign { get; set; } = true;

    /// <summary>Интеграция настроена и включена.</summary>
    public bool Enabled =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ServiceToken);
}
