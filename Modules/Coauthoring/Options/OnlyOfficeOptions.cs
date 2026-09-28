namespace delosfera_server.Modules.Coauthoring.Options;

/// <summary>
/// Подключение к OnlyOffice Document Server (COOP-1). Секреты (JwtSecret) задаются
/// через env / dotnet user-secrets, а не хранятся в appsettings.json — тем же
/// способом, что Jwt:Key и Minio:SecretKey (см. appsettings.example.json).
/// </summary>
public class OnlyOfficeOptions
{
    public const string Section = "OnlyOffice";

    /// <summary>Адрес Document Server, откуда клиент грузит api.js (напр. https://ds.keremetbank.kg).</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Общий секрет подписи JWT между нашим API и Document Server. Им подписывается
    /// конфиг редактора и проверяется тело callback'а. Пустой секрет = выключенная
    /// интеграция (см. CoauthoringService: конфиг не выдаётся).
    /// </summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>
    /// Публичный адрес НАШЕГО API, по которому Document Server забирает файл и шлёт
    /// callback (напр. https://delosfera.keremetbank.kg). DS ходит на него server-to-server,
    /// поэтому localhost из браузера тут не подходит. Пусто — берём базовый адрес запроса.
    /// </summary>
    public string ApiPublicUrl { get; set; } = string.Empty;

    /// <summary>Срок жизни подписанной ссылки на скачивание файла для DS (секунды).</summary>
    public int DocumentUrlTtlSeconds { get; set; } = 300;

    /// <summary>Интеграция настроена (есть адрес сервера и секрет).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(JwtSecret);
}
