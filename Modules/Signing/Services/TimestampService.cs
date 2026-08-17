using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Итог обращения к службе меток времени.</summary>
public class TimestampResult
{
    /// <summary>Метка получена и проверена.</summary>
    public bool Obtained { get; init; }

    /// <summary>Метка не запрашивалась — в настройках выключена.</summary>
    public bool Disabled { get; init; }

    /// <summary>Токен метки целиком: он и есть доказательство, его надо хранить.</summary>
    public byte[]? Token { get; init; }

    /// <summary>Время, которое удостоверила служба, — не наше и не время сервера.</summary>
    public DateTime? At { get; init; }

    /// <summary>Кто удостоверил.</summary>
    public string? Authority { get; init; }

    /// <summary>Почему метки нет.</summary>
    public string? Reason { get; init; }

    public static TimestampResult Off() => new() {Disabled = true};
    public static TimestampResult Fail(string reason) => new() {Reason = reason};
}

public interface ITimestampService
{
    /// <summary>
    /// Получить метку времени на подпись. Штампуется сама подпись, а не документ:
    /// метка утверждает «эта подпись существовала к этому моменту».
    /// </summary>
    Task<TimestampResult> StampAsync(byte[] signature, CancellationToken ct = default);
}

/// <summary>
/// Метка времени по RFC 3161 (Б-18).
///
/// Зачем она нужна: сертификат действует ограниченный срок, а документ живёт годами.
/// Без метки, когда срок истечёт, две картины станут неразличимы — подпись поставили,
/// пока сертификат действовал, и подпись поставили после. Метка от независимой службы
/// закрывает этот вопрос: время удостоверяет она, а не наш сервер, часы которого
/// администратор может перевести.
///
/// Служба у банка своя — у удостоверяющего центра, поэтому адрес в настройках.
/// Отказ службы не обязательно означает отказ подписи: это выбор банка, и он
/// вынесен в настройку. Молча подписать без метки, когда она включена, нельзя —
/// подпись будет выглядеть защищённой, не будучи ею.
/// </summary>
public class TimestampService : ITimestampService
{
    private readonly DelosferaDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<TimestampService> _logger;

    public TimestampService(
        DelosferaDbContext db, IHttpClientFactory http, ILogger<TimestampService> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task<TimestampResult> StampAsync(byte[] signature, CancellationToken ct = default)
    {
        var settings = await _db.SigningSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        if (settings is null || !settings.TimestampEnabled) return TimestampResult.Off();

        if (string.IsNullOrWhiteSpace(settings.TimestampAuthorityUrl))
            return TimestampResult.Fail(
                "Метка времени включена, но адрес службы не задан — укажите его в настройках подписи");

        try
        {
            // Запрашивается метка на хеш подписи, а не на саму подпись: служба
            // не должна видеть подписанное, ей достаточно свёртки.
            var request = Rfc3161TimestampRequest.CreateFromHash(
                SHA256.HashData(signature),
                HashAlgorithmName.SHA256,
                requestSignerCertificates: true);

            var client = _http.CreateClient("timestamp");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimestampTimeoutSeconds, 3, 120));

            using var content = new ByteArrayContent(request.Encode());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");

            using var response = await client.PostAsync(settings.TimestampAuthorityUrl, content, ct);

            if (!response.IsSuccessStatusCode)
                return TimestampResult.Fail(
                    $"Служба меток времени ответила {(int) response.StatusCode}");

            var body = await response.Content.ReadAsByteArrayAsync(ct);

            // Токен проверяется здесь же — ProcessResponse сверяет его с нашим запросом
            // и бросает, если не сходится. Непроверенная метка не лучше отсутствующей,
            // а разобрать её потом будет уже нечем: запрос не сохраняется.
            var token = request.ProcessResponse(body, out _);

            var info = token.TokenInfo;
            var cms = token.AsSignedCms();
            var signer = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0].Certificate : null;

            _logger.LogInformation(
                "Метка времени получена: {At} от {Authority}", info.Timestamp, signer?.Subject ?? "неизвестной службы");

            return new TimestampResult
            {
                Obtained = true,
                Token = body,
                At = info.Timestamp.UtcDateTime,
                Authority = Describe(signer),
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return TimestampResult.Fail("Служба меток времени не ответила за отведённое время");
        }
        catch (HttpRequestException ex)
        {
            return TimestampResult.Fail($"Служба меток времени недоступна: {ex.Message}");
        }
        catch (CryptographicException ex)
        {
            return TimestampResult.Fail($"Метка времени не проверена: {ex.Message}");
        }
    }

    /// <summary>Из полного имени службы оставить то, что человеку что-то говорит.</summary>
    private static string? Describe(X509Certificate2? certificate)
    {
        if (certificate is null) return null;

        var cn = certificate.Subject
            .Split(',', StringSplitOptions.TrimEntries)
            .FirstOrDefault(part => part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase));

        return cn is null ? certificate.Subject : cn[3..].Trim();
    }
}
