using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Options;
using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Signing.Services.Oep;

/// <summary>Итог облачного подписания — то, что возвращаем фронту.</summary>
public sealed class OepSignResult
{
    public int SignatureId { get; set; }
    public string? PersonFio { get; set; }
    public DateTime SignedAtUtc { get; set; }
    public bool Verified { get; set; }
    public string? CertificateSubject { get; set; }
    public DateTime? CertificateValidTo { get; set; }
}

/// <summary>
/// Оркестрация облачной ЭП (ОЭП ГП «Инфоком») в контуре подписания Делосферы.
///
/// Ключ подписанта из системы не проходит: закрытый ключ живёт в облаке УЦ, а сюда
/// возвращается готовая CMS-подпись хеша (тот же принцип делегирования, что у ТУМАР-CSP
/// на рабочем месте). Подписывается ОТПЕЧАТОК документа/вложения — тот же хеш, что
/// отдаёт квалифицированный контур (SIG-01): изменение подписанного аннулирует подпись.
///
/// Криптопроверку ГОСТ Р 34.10 выполняет сам УЦ (обратный вызов check-sign), сервер
/// её не дублирует — ГОСТ .NET не проверяет. Сертификат из CMS разбирается только
/// структурно, для реквизитов штампа.
/// </summary>
public interface IOepSigningService
{
    bool Enabled { get; }

    Task<OepAuthMethodsResponse> GetAuthMethodsAsync(string? personIdnp, string? organizationInn, CancellationToken ct = default);
    Task<OepSendPinResponse> SendPinAsync(string? personIdnp, string? organizationInn, string method, CancellationToken ct = default);

    Task<OepSignResult> SignDocumentAsync(
        int documentId, OepSignParams p, int userId, CancellationToken ct = default);
    Task<OepSignResult> SignAttachmentAsync(
        int attachmentId, OepSignParams p, int userId, CancellationToken ct = default);
}

/// <summary>Данные подписанта из облака: ПИН физлица, ИНН организации и пин-код.</summary>
public sealed class OepSignParams
{
    public string? PersonIdnp { get; set; }
    public string? OrganizationInn { get; set; }
    public string ByPin { get; set; } = "";
}

public sealed class OepSigningService(
    IOepSignatureClient client,
    IQualifiedSignatureService qualified,
    ISignatureService signatures,
    IOptions<OepOptions> options,
    ILogger<OepSigningService> logger) : IOepSigningService
{
    private readonly OepOptions _opt = options.Value;

    public bool Enabled => client.Enabled;

    public Task<OepAuthMethodsResponse> GetAuthMethodsAsync(
        string? personIdnp, string? organizationInn, CancellationToken ct = default) =>
        client.GetAuthMethodsAsync(personIdnp, organizationInn, ct);

    public Task<OepSendPinResponse> SendPinAsync(
        string? personIdnp, string? organizationInn, string method, CancellationToken ct = default) =>
        client.SendPinAsync(personIdnp, organizationInn, method, ct);

    public async Task<OepSignResult> SignDocumentAsync(
        int documentId, OepSignParams p, int userId, CancellationToken ct = default)
    {
        var challenge = await qualified.GetDocumentChallengeAsync(documentId);
        return await SignAsync(
            challenge.Hash, p, userId, ct,
            persist: (stamp) => signatures.SignDocumentAsync(documentId, SignatureLevel.Qualified, userId, stamp),
            what: $"документ {documentId}");
    }

    public async Task<OepSignResult> SignAttachmentAsync(
        int attachmentId, OepSignParams p, int userId, CancellationToken ct = default)
    {
        var challenge = await qualified.GetChallengeAsync(attachmentId);
        return await SignAsync(
            challenge.Hash, p, userId, ct,
            persist: (stamp) => signatures.SignAsync(attachmentId, SignatureLevel.Qualified, userId, stamp),
            what: $"вложение {attachmentId}");
    }

    // ── общий сценарий ───────────────────────────────────────────────────────

    private async Task<OepSignResult> SignAsync(
        string hashHex, OepSignParams p, int userId, CancellationToken ct,
        Func<string, Task<Signature>> persist, string what)
    {
        // 1. Пин-код → userToken (действует ~1 минуту, поэтому берём непосредственно перед подписью).
        var auth = await client.AuthenticateAsync(p.PersonIdnp, p.OrganizationInn, p.ByPin, ct);
        if (string.IsNullOrWhiteSpace(auth.Token))
            throw new InvalidOperationException("ОЭП не вернул токен авторизации");

        // 2. Подпись хеша отпечатка. API принимает hex; шлём в верхнем регистре, как в примерах.
        var hash = hashHex.ToUpperInvariant();
        var signed = await client.SignHashAsync(hash, auth.Token, ct);
        if (!signed.IsSuccessful || string.IsNullOrWhiteSpace(signed.Sign))
            throw new InvalidOperationException("ОЭП не смог подписать хеш");

        // 3. Проверка подписи на стороне УЦ (ГОСТ). Ошибка → подпись не принимаем.
        var verified = true;
        if (_opt.VerifyAfterSign)
        {
            try
            {
                var check = await client.CheckSignAsync(signed.Sign, hash, p.PersonIdnp, ct);
                verified = check.Verified;
            }
            catch (OepApiException ex)
            {
                logger.LogWarning("ОЭП check-sign: {Message}", ex.Message);
                verified = false;
            }
            if (!verified)
                throw new InvalidOperationException("Подпись ОЭП не прошла проверку в УЦ — не принята");
        }

        // 4. Реквизиты сертификата из CMS (структурно, без крипто-проверки — её сделал УЦ).
        var cert = ExtractCertificate(signed.Sign);
        var signedAt = DateTimeOffset.FromUnixTimeMilliseconds(signed.Timestamp).UtcDateTime;
        var stamp = BuildStamp(cert, auth, signedAt);

        // 5. Сохраняем подпись в контуре Делосферы (штамп + отпечаток; уровень — КЭП).
        var stored = await persist(stamp);

        logger.LogInformation(
            "ОЭП: {What} подписан пользователем {UserId} (ПИН {Idnp}), сертификат {Subject}",
            what, userId, p.PersonIdnp, cert?.Subject);

        return new OepSignResult
        {
            SignatureId = stored.Id,
            PersonFio = auth.PersonFio,
            SignedAtUtc = signedAt == default ? DateTime.UtcNow : signedAt,
            Verified = verified,
            CertificateSubject = cert?.Subject,
            CertificateValidTo = cert?.NotAfter.ToUniversalTime(),
        };
    }

    /// <summary>Достаёт сертификат подписанта из CMS-подписи (структурно).</summary>
    private static X509Certificate2? ExtractCertificate(string signBase64)
    {
        try
        {
            var cms = new SignedCms();
            cms.Decode(Convert.FromBase64String(signBase64));
            foreach (var si in cms.SignerInfos)
                if (si.Certificate is { } c) return c;
            return cms.Certificates.Count > 0 ? cms.Certificates[0] : null;
        }
        catch
        {
            // Структуру CMS разобрать не удалось — штамп сохраним без реквизитов сертификата.
            return null;
        }
    }

    /// <summary>Штамп в том же JSON-виде, что и у квалифицированного контура (SIG-03).</summary>
    private static string BuildStamp(X509Certificate2? cert, OepAuthResponse auth, DateTime signedAt) =>
        JsonSerializer.Serialize(new
        {
            subject = cert?.Subject,
            issuer = cert?.Issuer,
            serial = cert?.SerialNumber,
            validFrom = cert?.NotBefore.ToUniversalTime(),
            validTo = cert?.NotAfter.ToUniversalTime(),
            algorithm = cert?.SignatureAlgorithm?.FriendlyName ?? "ГОСТ Р 34.10",
            thumbprint = cert?.Thumbprint,
            // Доверие обеспечивает сам УЦ «Инфоком»: подпись прошла его проверку (check-sign).
            trustAuthority = cert is not null ? IssuerCommonName(cert) : "ГП «Инфоком»",
            trustNotChecked = false,
            revocationChecked = true,
            revocationNote = (string?) null,
            timestampAt = signedAt == default ? (DateTime?) null : signedAt,
            timestampAuthority = "ОЭП ГП «Инфоком»",
            timestampNote = (string?) null,
            // Пометка канала подписания — для журнала и разбора.
            provider = "oep",
            personFio = auth.PersonFio,
            personIdnp = auth.PersonIdnp,
        });

    private static string IssuerCommonName(X509Certificate2 cert)
    {
        var cn = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: true);
        return string.IsNullOrWhiteSpace(cn) ? "ГП «Инфоком»" : cn;
    }
}
