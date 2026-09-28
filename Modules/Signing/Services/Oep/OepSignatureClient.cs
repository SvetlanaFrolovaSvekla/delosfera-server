using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace delosfera_server.Modules.Signing.Services.Oep;

/// <summary>
/// HTTP-клиент API ОЭП ГП «Инфоком». Каждый запрос — POST JSON с Bearer сервис-токена
/// организации и User-Agent = наименование организации (п.6 ограничений). Ответ либо
/// целевой объект, либо конверт ошибки {errorCode, errorMessage} — второй превращается
/// в <see cref="OepApiException"/>.
/// </summary>
public sealed class OepSignatureClient : IOepSignatureClient
{
    private readonly HttpClient _http;
    private readonly OepOptions _opt;
    private readonly ILogger<OepSignatureClient> _logger;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public OepSignatureClient(HttpClient http, IOptions<OepOptions> opt, ILogger<OepSignatureClient> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;

        if (_opt.Enabled)
        {
            _http.BaseAddress = new Uri(_opt.BaseUrl.TrimEnd('/') + "/");
            _http.Timeout = TimeSpan.FromSeconds(_opt.TimeoutSeconds);
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ServiceToken);
            if (!string.IsNullOrWhiteSpace(_opt.OrganizationName))
                _http.DefaultRequestHeaders.UserAgent.ParseAdd(SanitizeUserAgent(_opt.OrganizationName));
        }
    }

    public bool Enabled => _opt.Enabled;

    public Task<OepAuthMethodsResponse> GetAuthMethodsAsync(
        string? personIdnp, string? organizationInn, CancellationToken ct = default) =>
        PostAsync<OepAuthMethodsResponse>("user-auth-methods",
            new OepAuthMethodsRequest { PersonIdnp = personIdnp, OrganizationInn = OrgOrDefault(organizationInn) }, ct);

    public Task<OepSendPinResponse> SendPinAsync(
        string? personIdnp, string? organizationInn, string method, CancellationToken ct = default) =>
        PostAsync<OepSendPinResponse>("get-pin-code",
            new OepSendPinRequest
            {
                PersonIdnp = personIdnp, OrganizationInn = OrgOrDefault(organizationInn), Method = method,
            }, ct);

    public Task<OepAuthResponse> AuthenticateAsync(
        string? personIdnp, string? organizationInn, string byPin, CancellationToken ct = default) =>
        PostAsync<OepAuthResponse>("account/auth",
            new OepAuthRequest { PersonIdnp = personIdnp, OrganizationInn = OrgOrDefault(organizationInn), ByPin = byPin }, ct);

    public Task<OepCertInfoResponse> GetCertInfoAsync(string userToken, CancellationToken ct = default) =>
        PostAsync<OepCertInfoResponse>("get-cert-info", new OepCertInfoRequest { UserToken = userToken }, ct);

    public Task<OepSignHashResponse> SignHashAsync(string hashHex, string userToken, CancellationToken ct = default) =>
        PostAsync<OepSignHashResponse>("get-sign/for-hash",
            new OepSignHashRequest { Hash = hashHex, UserToken = userToken }, ct);

    public Task<OepCheckSignResponse> CheckSignAsync(
        string signBase64, string hashHex, string? personIdnp, CancellationToken ct = default) =>
        PostAsync<OepCheckSignResponse>("check-sign/for-hash",
            new OepCheckSignRequest { SignBase64 = signBase64, Hash = hashHex, PersonIdnp = personIdnp }, ct);

    // ── общая часть ────────────────────────────────────────────────────────────

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        if (!_opt.Enabled)
            throw new InvalidOperationException("Облачная ЭП (ОЭП) не настроена: задайте Oep:BaseUrl и Oep:ServiceToken");

        using var resp = await _http.PostAsJsonAsync(path, body, Json, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);

        // Конверт ошибки может прийти и с кодом 200, и с 4xx/5xx — распознаём по errorCode.
        var error = TryReadError(raw);
        if (error is not null)
        {
            _logger.LogWarning("ОЭП {Path}: ошибка {Code} {Message}", path, error.ErrorCode, error.ErrorMessage);
            throw new OepApiException(error.ErrorCode, error.ErrorMessage ?? "Ошибка API ОЭП");
        }

        if (!resp.IsSuccessStatusCode)
            throw new OepApiException((int) resp.StatusCode, $"API ОЭП вернул {(int) resp.StatusCode}");

        var result = string.IsNullOrWhiteSpace(raw) ? default : JsonSerializer.Deserialize<T>(raw, Json);
        return result ?? throw new OepApiException(502, "Пустой ответ API ОЭП");
    }

    private static OepErrorResponse? TryReadError(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains("errorCode", StringComparison.Ordinal)) return null;
        try
        {
            var err = JsonSerializer.Deserialize<OepErrorResponse>(raw, Json);
            return err is { ErrorCode: > 0 } ? err : null;
        }
        catch { return null; }
    }

    private string? OrgOrDefault(string? organizationInn) =>
        string.IsNullOrWhiteSpace(organizationInn) ? _opt.OrganizationInn : organizationInn;

    /// <summary>User-Agent без спец-символов (требование п.6 ограничений API).</summary>
    private static string SanitizeUserAgent(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
        return string.IsNullOrEmpty(cleaned) ? "Delosfera" : cleaned;
    }
}
