using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Данные, которые криптопровайдер на рабочем месте должен подписать.</summary>
public class SignChallengeDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>Хеш версии файла (hex) — именно он подписывается, а не сам файл.</summary>
    public string Hash { get; set; } = string.Empty;

    public string HashAlgorithm { get; set; } = "SHA-256";

    /// <summary>Тот же хеш в base64 — в таком виде его принимает браузерный плагин.</summary>
    public string DataToSign { get; set; } = string.Empty;

    /// <summary>Уже наложенные действующие подписи.</summary>
    public List<SignatureInfoDto> Signatures { get; set; } = [];
}

public class SignatureInfoDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public SignatureLevel Level { get; set; }
    public string LevelTitle { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public bool Revoked { get; set; }
    public string? RevokedReason { get; set; }

    /// <summary>Реквизиты сертификата для визуального штампа (SIG-03).</summary>
    public string? CertificateSubject { get; set; }
    public string? CertificateSerial { get; set; }
    public DateTime? CertificateValidTo { get; set; }
}

public class QualifiedSignRequest
{
    /// <summary>Подпись хеша, полученная от криптопровайдера, base64.</summary>
    public required string Signature { get; set; }

    /// <summary>Сертификат подписанта (DER или PEM), base64.</summary>
    public required string Certificate { get; set; }
}

public interface IQualifiedSignatureService
{
    Task<SignChallengeDto> GetChallengeAsync(int attachmentId);
    Task<SignatureInfoDto> SignAsync(int attachmentId, QualifiedSignRequest request, int userId);
}

/// <summary>
/// Квалифицированная ЭП (SIG-02, INT-03).
///
/// Закрытый ключ остаётся на рабочем месте подписанта: система отдаёт хеш версии,
/// криптопровайдер (ТУМАР-CSP либо облачная ЭП УЦ «Инфоком») подписывает его, а
/// сюда возвращается только подпись и сертификат. Ключ через сервер не проходит —
/// иначе банк отвечал бы за его хранение, а подпись перестала бы быть личной.
///
/// Подписывается хеш конкретной версии файла (SIG-01): изменение файла аннулирует
/// подпись, потому что новый хеш проверку уже не пройдёт.
///
/// Проверка алгоритмов ограничена RSA и ECDSA — тем, что умеет .NET. Сертификаты
/// ГОСТ Р 34.10, которые выдаёт УЦ «Инфоком», проверяются самим ТУМАР-CSP на стороне
/// рабочего места; серверная проверка ГОСТ требует отдельного СКЗИ и включается
/// адаптером на этапе обследования. Отказ здесь честнее, чем принять подпись,
/// которую система не в состоянии проверить.
/// </summary>
public class QualifiedSignatureService : IQualifiedSignatureService
{
    private readonly DelosferaDbContext _db;
    private readonly ISignatureService _signatures;
    private readonly ILogger<QualifiedSignatureService> _logger;

    public QualifiedSignatureService(
        DelosferaDbContext db,
        ISignatureService signatures,
        ILogger<QualifiedSignatureService> logger)
    {
        _db = db;
        _signatures = signatures;
        _logger = logger;
    }

    public async Task<SignChallengeDto> GetChallengeAsync(int attachmentId)
    {
        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        return new SignChallengeDto
        {
            AttachmentId = attachment.Id,
            FileName = attachment.FileName,
            Hash = attachment.Hash,
            DataToSign = Convert.ToBase64String(HexToBytes(attachment.Hash)),
            Signatures = await LoadSignaturesAsync(attachmentId),
        };
    }

    public async Task<SignatureInfoDto> SignAsync(
        int attachmentId, QualifiedSignRequest request, int userId)
    {
        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        var certificate = ParseCertificate(request.Certificate);
        var signature = ParseBase64(request.Signature, "подпись");
        var hash = HexToBytes(attachment.Hash);

        var now = DateTime.UtcNow;

        if (now < certificate.NotBefore.ToUniversalTime())
            throw new InvalidOperationException(
                $"Сертификат вступает в силу {certificate.NotBefore:dd.MM.yyyy} — подписание невозможно");

        if (now > certificate.NotAfter.ToUniversalTime())
            throw new InvalidOperationException(
                $"Срок действия сертификата истёк {certificate.NotAfter:dd.MM.yyyy}");

        if (!Verify(certificate, hash, signature))
            throw new InvalidOperationException(
                "Подпись не соответствует хешу версии файла или сертификату подписанта");

        var stamp = JsonSerializer.Serialize(new
        {
            subject = certificate.Subject,
            issuer = certificate.Issuer,
            serial = certificate.SerialNumber,
            validFrom = certificate.NotBefore.ToUniversalTime(),
            validTo = certificate.NotAfter.ToUniversalTime(),
            algorithm = certificate.SignatureAlgorithm.FriendlyName,
            thumbprint = certificate.Thumbprint,
        });

        var stored = await _signatures.SignAsync(attachmentId, SignatureLevel.Qualified, userId, stamp);

        _logger.LogInformation(
            "КЭП: вложение {AttachmentId} подписано пользователем {UserId}, сертификат {Subject}",
            attachmentId, userId, certificate.Subject);

        var user = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync();

        return ToDto(stored, user, certificate);
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    /// <summary>
    /// Проверка подписи хеша. Плагины отдают либо «сырую» подпись хеша, либо
    /// контейнер CMS/PKCS#7 — принимаем оба вида, потому что состав средств ЭП
    /// у банка фиксируется только на этапе обследования.
    /// </summary>
    private bool Verify(X509Certificate2 certificate, byte[] hash, byte[] signature)
    {
        if (TryVerifyCms(hash, signature)) return true;

        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is not null &&
            rsa.VerifyHash(hash, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            return true;

        using var ecdsa = certificate.GetECDsaPublicKey();
        if (ecdsa is not null && ecdsa.VerifyHash(hash, signature))
            return true;

        if (rsa is null && ecdsa is null)
            throw new InvalidOperationException(
                "Алгоритм сертификата не поддерживается серверной проверкой. " +
                "Для сертификатов ГОСТ подключается адаптер СКЗИ (ТУМАР-CSP) — см. SIG-02");

        return false;
    }

    private static bool TryVerifyCms(byte[] hash, byte[] signature)
    {
        try
        {
            var cms = new System.Security.Cryptography.Pkcs.SignedCms(
                new System.Security.Cryptography.Pkcs.ContentInfo(hash), detached: true);

            cms.Decode(signature);

            // Цепочку до корня здесь не строим: доверенные УЦ ставятся на серверах
            // банка отдельно, и на этапе обследования список корней ещё не согласован.
            cms.CheckSignature(verifySignatureOnly: true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private async Task<List<SignatureInfoDto>> LoadSignaturesAsync(int attachmentId)
    {
        var rows = await _db.Signatures
            .Where(s => s.DocumentAttachmentId == attachmentId)
            .OrderBy(s => s.At)
            .ToListAsync();

        var userIds = rows.Select(s => s.UserId).Distinct().ToList();
        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return rows.Select(s => ToDto(s, names.GetValueOrDefault(s.UserId), null)).ToList();
    }

    private static SignatureInfoDto ToDto(Signature s, string? userName, X509Certificate2? certificate)
    {
        var dto = new SignatureInfoDto
        {
            Id = s.Id,
            UserId = s.UserId,
            UserName = userName,
            Level = s.Level,
            LevelTitle = s.Level == SignatureLevel.Qualified
                ? "Квалифицированная ЭП"
                : "Простая ЭП",
            At = s.At,
            Revoked = s.Revoked,
            RevokedReason = s.RevokedReason,
        };

        if (certificate is not null)
        {
            dto.CertificateSubject = certificate.Subject;
            dto.CertificateSerial = certificate.SerialNumber;
            dto.CertificateValidTo = certificate.NotAfter.ToUniversalTime();
            return dto;
        }

        // Реквизиты сертификата лежат в метаданных штампа — читаем их для печатной формы.
        if (string.IsNullOrWhiteSpace(s.StampMeta)) return dto;

        try
        {
            using var doc = JsonDocument.Parse(s.StampMeta);
            var root = doc.RootElement;

            if (root.TryGetProperty("subject", out var subject)) dto.CertificateSubject = subject.GetString();
            if (root.TryGetProperty("serial", out var serial)) dto.CertificateSerial = serial.GetString();
            if (root.TryGetProperty("validTo", out var validTo) && validTo.TryGetDateTime(out var to))
                dto.CertificateValidTo = to;
        }
        catch (JsonException)
        {
            // Штамп мог быть записан в свободной форме более ранней версией — не повод падать.
        }

        return dto;
    }

    private static X509Certificate2 ParseCertificate(string raw)
    {
        var bytes = ParseBase64(StripPem(raw), "сертификат");

        try
        {
            return X509CertificateLoader.LoadCertificate(bytes);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException($"Сертификат не разобран: {ex.Message}");
        }
    }

    private static string StripPem(string raw) => raw
        .Replace("-----BEGIN CERTIFICATE-----", string.Empty)
        .Replace("-----END CERTIFICATE-----", string.Empty)
        .Replace("\r", string.Empty)
        .Replace("\n", string.Empty)
        .Trim();

    private static byte[] ParseBase64(string value, string what)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"Не удалось разобрать {what}: ожидается base64");
        }
    }

    private static byte[] HexToBytes(string hex)
    {
        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Хеш версии файла записан не в шестнадцатеричном виде — подписание невозможно");
        }
    }
}
