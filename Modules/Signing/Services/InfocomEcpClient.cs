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

    /// <summary>ecp-service внутри изолированной docker-сети (10.222.10.42 → 185.88.32.25).
    /// http:// (не https) — допущение, что в этой сети нет посторонних контейнеров/узлов. Если
    /// сеть когда-нибудь расширится за пределы доверенного периметра (например, ecp-service
    /// переедет в другой сегмент или появится shared-хостинг), это нужно пересмотреть на TLS.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5190";
    public string AccessToken { get; set; } = "";

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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public InfocomEcpClient(HttpClient http, IOptions<InfocomEcpServiceOptions> options)
    {
        var settings = options.Value;
        _http = http;
        _http.BaseAddress = new Uri(settings.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);
    }

    public Task<JsonElement> UserAuthMethodsAsync(InfocomUserAuthMethodsRequest request, CancellationToken ct) =>
        PostAsync("/api/user-auth-methods", request, ct);

    public Task<JsonElement> GetPinCodeAsync(InfocomGetPinCodeRequest request, CancellationToken ct) =>
        PostAsync("/api/get-pin-code", request, ct);

    public Task<JsonElement> AccountAuthAsync(InfocomAccountAuthRequest request, CancellationToken ct) =>
        PostAsync("/api/account/auth", request, ct);

    public Task<JsonElement> GetCertInfoAsync(InfocomGetCertInfoRequest request, CancellationToken ct) =>
        PostAsync("/api/get-cert-info", request, ct);

    public Task<JsonElement> SignHashAsync(InfocomSignHashRequest request, CancellationToken ct) =>
        PostAsync("/api/get-sign/for-hash", request, ct);

    public Task<JsonElement> CheckSignAsync(InfocomCheckSignRequest request, CancellationToken ct) =>
        PostAsync("/api/check-sign/for-hash", request, ct);

    private async Task<JsonElement> PostAsync<TRequest>(string path, TRequest body, CancellationToken ct)
    {
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
