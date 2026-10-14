using System.Net.Http.Headers;
using System.Text.Json;
using delosfera_server.Modules.Signing.Models.InfocomEcp;
using Microsoft.Extensions.Options;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Адрес и токен delosfera-ecp-service (см. appsettings.json → InfocomEcpService).
/// Не путать с токеном самого ГП Инфоком — тот хранится внутри ecp-service и сюда не попадает.</summary>
public class InfocomEcpServiceOptions
{
    public const string Section = "InfocomEcpService";

    /// <summary>Адрес delosfera-ecp-service. По этому каналу идут ПИН, коды из СМС, токены сотрудников и
    /// сервисный токен, поэтому он должен быть https:// (см. <see cref="AllowInsecureHttp"/>). http:// без
    /// флага допускается только на localhost (локальная разработка).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5190";
    public string AccessToken { get; set; } = "";

    /// <summary>Разрешить http:// не на localhost. Включать ТОЛЬКО осознанно, когда сервер и ecp-service
    /// общаются внутри одной изолированной docker-сети на одном хосте и трафик не выходит в общую сеть.
    /// По умолчанию false: запрос к ecp-service по открытому каналу не отправляется вовсе.</summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>SHA-256 отпечаток сертификата ecp-service (hex, двоеточия и пробелы допустимы) — для
    /// самоподписанного сертификата без корпоративного УЦ. Если задан, принимается ТОЛЬКО сертификат с этим
    /// отпечатком (certificate pinning), проверка цепочки и имени не выполняется. Пусто — обычная проверка
    /// сертификата (цепочка до доверенного УЦ + имя хоста).</summary>
    public string ServerCertificateSha256 { get; set; } = "";

    /// <summary>15 c, а не дефолтные 100 c HttpClient: зависший вызов к ecp-service не должен
    /// надолго занимать поток запроса пользователя.</summary>
    public int TimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// Клиент к delosfera-ecp-service — сервису-прокладке к ГП «Инфоком» (SIG-ECP). Бекенд ходит
/// только сюда (роль 10.222.10.42 → 185.88.32.25 в сетевой схеме интеграции), сам к
/// cdsapi-test.srs.kg не обращается.
/// </summary>
public interface IInfocomEcpClient
{
    Task<JsonElement> UserAuthMethodsAsync(InfocomUserAuthMethodsRequest request, CancellationToken ct);
    Task<JsonElement> GetPinCodeAsync(InfocomGetPinCodeRequest request, CancellationToken ct);
    Task<JsonElement> AccountAuthAsync(InfocomAccountAuthRequest request, CancellationToken ct);
    Task<JsonElement> GetCertInfoAsync(InfocomGetCertInfoRequest request, CancellationToken ct);
    Task<JsonElement> SignHashAsync(InfocomSignHashRequest request, CancellationToken ct);
    Task<JsonElement> CheckSignAsync(InfocomCheckSignRequest request, CancellationToken ct);
}

/// <summary>Ошибка ecp-service/ГП Инфоком с уже разобранным телом — контроллер отвечает клиенту
/// тем же статусом и тем же errorCode/errorMessage, без превращения в общий 500.</summary>
public class InfocomEcpException(int statusCode, InfocomErrorBody error) : Exception(error.ErrorMessage ?? "Ошибка сервиса ЭЦП")
{
    public int StatusCode { get; } = statusCode;
    public InfocomErrorBody Error { get; } = error;
}

public class InfocomEcpClient : IInfocomEcpClient
{
    private readonly HttpClient _http;
    private readonly string? _insecureReason;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public InfocomEcpClient(HttpClient http, IOptions<InfocomEcpServiceOptions> options)
    {
        var settings = options.Value;
        _http = http;
        _http.BaseAddress = new Uri(settings.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        _insecureReason = InsecureChannelReason(_http.BaseAddress, settings.AllowInsecureHttp);
    }

    /// <summary>null — канал допустим: https://, либо http:// на localhost, либо http:// с явным
    /// AllowInsecureHttp. Иначе — текст причины отказа.</summary>
    public static string? InsecureChannelReason(Uri baseAddress, bool allowInsecureHttp)
    {
        if (baseAddress.Scheme == Uri.UriSchemeHttps || baseAddress.IsLoopback || allowInsecureHttp) return null;
        return "Канал к delosfera-ecp-service не защищён: InfocomEcpService:BaseUrl должен быть https:// " +
               "(или явно разрешите http внутри изолированной docker-сети: InfocomEcpService:AllowInsecureHttp=true)";
    }

    public Task<JsonElement> UserAuthMethodsAsync(InfocomUserAuthMethodsRequest request, CancellationToken ct) =>
        PostAsync("api/user-auth-methods", request, ct);

    public Task<JsonElement> GetPinCodeAsync(InfocomGetPinCodeRequest request, CancellationToken ct) =>
        PostAsync("api/get-pin-code", request, ct);

    public Task<JsonElement> AccountAuthAsync(InfocomAccountAuthRequest request, CancellationToken ct) =>
        PostAsync("api/account/auth", request, ct);

    public Task<JsonElement> GetCertInfoAsync(InfocomGetCertInfoRequest request, CancellationToken ct) =>
        PostAsync("api/get-cert-info", request, ct);

    public Task<JsonElement> SignHashAsync(InfocomSignHashRequest request, CancellationToken ct) =>
        PostAsync("api/get-sign/for-hash", request, ct);

    public Task<JsonElement> CheckSignAsync(InfocomCheckSignRequest request, CancellationToken ct) =>
        PostAsync("api/check-sign/for-hash", request, ct);

    private async Task<JsonElement> PostAsync<TRequest>(string path, TRequest body, CancellationToken ct)
    {
        // ПИН, коды и токены по открытому каналу не отправляем вовсе (fail closed). 503 — вызывающий код
        // трактует это как недоступность сервиса ЭЦП, решение "Согласовать" не сохранится.
        if (_insecureReason is not null)
            throw new InfocomEcpException(StatusCodes.Status503ServiceUnavailable,
                new InfocomErrorBody { ErrorCode = 503, ErrorMessage = _insecureReason });

        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            InfocomErrorBody error;
            try
            {
                error = JsonSerializer.Deserialize<InfocomErrorBody>(raw, JsonOptions)
                        ?? new InfocomErrorBody { ErrorCode = (int)response.StatusCode, ErrorMessage = raw };
            }
            catch (JsonException)
            {
                error = new InfocomErrorBody { ErrorCode = (int)response.StatusCode, ErrorMessage = raw };
            }

            throw new InfocomEcpException((int)response.StatusCode, error);
        }

        using var doc = JsonDocument.Parse(string.IsNullOrEmpty(raw) ? "{}" : raw);
        return doc.RootElement.Clone();
    }
}
