using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Данные, которые криптопровайдер на рабочем месте должен подписать.</summary>
public class SignChallengeDto
{
    /// <summary>Подписываемое вложение. Пусто, когда подписывается карточка документа.</summary>
    public int? AttachmentId { get; set; }

    /// <summary>Подписываемый документ. Пусто, когда подписывается отдельное вложение.</summary>
    public int? DocumentId { get; set; }

    /// <summary>Что человек увидит перед тем, как приложить ключ.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Хеш версии (hex) — именно он подписывается, а не сам файл.</summary>
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

    /// <summary>Каким удостоверяющим центром подтверждён сертификат.</summary>
    public string? TrustAuthority { get; set; }

    /// <summary>Цепочка не проверялась — доверенные центры не заведены.</summary>
    public bool TrustNotChecked { get; set; }

    /// <summary>Отзыв сертификата проверен по списку удостоверяющего центра.</summary>
    public bool RevocationChecked { get; set; }

    /// <summary>Почему отзыв не проверен.</summary>
    public string? RevocationNote { get; set; }

    /// <summary>Время, удостоверённое службой меток. Пусто — метки нет.</summary>
    public DateTime? TimestampedAt { get; set; }

    public string? TimestampAuthority { get; set; }

    /// <summary>Почему метки нет.</summary>
    public string? TimestampNote { get; set; }
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

    /// <summary>Данные для подписи карточки документа целиком.</summary>
    Task<SignChallengeDto> GetDocumentChallengeAsync(int documentId);

    /// <summary>Принять квалифицированную подпись карточки документа.</summary>
    Task<SignatureInfoDto> SignDocumentAsync(int documentId, QualifiedSignRequest request, int userId);
}

/// <summary>
/// Квалифицированная ЭП (SIG-02, INT-03).
///
/// Закрытый ключ остаётся на рабочем месте подписанта: система отдаёт хеш версии,
/// криптопровайдер (ТУМАР-CSP либо облачная ЭП УЦ «Инфоком») подписывает его, а
/// сюда возвращается только подпись и сертификат. Ключ через сервер не проходит —
/// иначе банк отвечал бы за его хранение, а подпись перестала бы быть личной.
///
/// Подписывается хеш конкретной версии (SIG-01): у вложения это хеш файла, у карточки —
/// отпечаток её существенных полей вместе с хешами вложений. Изменение подписанного
/// аннулирует подпись, потому что новый хеш проверку уже не пройдёт.
///
/// Подпись принимается, только если сертификат прошёл три проверки: срок действия,
/// цепочку до корня, которому доверяет банк, и принадлежность подписанту. Последняя
/// нужна потому, что математически подпись сходится с любым действующим ключом —
/// и без неё чужой визой можно было бы закрыть чей угодно этап.
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
    private readonly ICertificateTrustService _trust;
    private readonly IDocumentFingerprintService _fingerprints;
    private readonly ITimestampService _timestamps;
    private readonly ILogger<QualifiedSignatureService> _logger;

    public QualifiedSignatureService(
        DelosferaDbContext db,
        ISignatureService signatures,
        ICertificateTrustService trust,
        IDocumentFingerprintService fingerprints,
        ITimestampService timestamps,
        ILogger<QualifiedSignatureService> logger)
    {
        _db = db;
        _signatures = signatures;
        _trust = trust;
        _fingerprints = fingerprints;
        _timestamps = timestamps;
        _logger = logger;
    }

    // ── вложение ─────────────────────────────────────────────────────────────

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
            Signatures = await LoadSignaturesAsync(s => s.DocumentAttachmentId == attachmentId),
        };
    }

    public async Task<SignatureInfoDto> SignAsync(
        int attachmentId, QualifiedSignRequest request, int userId)
    {
        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId)
            ?? throw new KeyNotFoundException("Вложение не найдено");

        var accepted = await AcceptAsync(request, HexToBytes(attachment.Hash), userId);
        var (certificate, trust, timestamp) = accepted;

        using (certificate)
        {
            var stored = await _signatures.SignAsync(
                attachmentId, SignatureLevel.Qualified, userId, BuildStamp(certificate, trust, timestamp));

            await SaveTimestampAsync(stored.Id, timestamp);

            _logger.LogInformation(
                "КЭП: вложение {AttachmentId} подписано пользователем {UserId}, сертификат {Subject}",
                attachmentId, userId, certificate.Subject);

            return ToDto(stored, await UserNameAsync(userId), certificate, trust, timestamp);
        }
    }

    // ── карточка документа ───────────────────────────────────────────────────

    public async Task<SignChallengeDto> GetDocumentChallengeAsync(int documentId)
    {
        var document = await _db.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new {d.Id, d.Title, d.RegNumber})
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Документ не найден");

        // Отпечаток карточки — свёртка её полей и хешей вложений: у служебной записки
        // подписывать файлом нечего, текст живёт в самой карточке.
        var fingerprint = await _fingerprints.ComputeAsync(documentId);
        var hash = FingerprintToBytes(fingerprint);

        return new SignChallengeDto
        {
            DocumentId = document.Id,
            FileName = string.IsNullOrWhiteSpace(document.RegNumber)
                ? document.Title
                : $"{document.RegNumber} · {document.Title}",
            Hash = Convert.ToHexString(hash).ToLowerInvariant(),
            DataToSign = Convert.ToBase64String(hash),
            Signatures = await LoadSignaturesAsync(s => s.DocumentId == documentId),
        };
    }

    public async Task<SignatureInfoDto> SignDocumentAsync(
        int documentId, QualifiedSignRequest request, int userId)
    {
        var exists = await _db.Documents.AnyAsync(d => d.Id == documentId);
        if (!exists) throw new KeyNotFoundException("Документ не найден");

        var fingerprint = await _fingerprints.ComputeAsync(documentId);
        var accepted = await AcceptAsync(request, FingerprintToBytes(fingerprint), userId);
        var (certificate, trust, timestamp) = accepted;

        using (certificate)
        {
            var stored = await _signatures.SignDocumentAsync(
                documentId, SignatureLevel.Qualified, userId, BuildStamp(certificate, trust, timestamp));

            await SaveTimestampAsync(stored.Id, timestamp);

            _logger.LogInformation(
                "КЭП: документ {DocumentId} подписан пользователем {UserId}, сертификат {Subject}",
                documentId, userId, certificate.Subject);

            return ToDto(stored, await UserNameAsync(userId), certificate, trust, timestamp);
        }
    }

    // ── общая часть приёма подписи ───────────────────────────────────────────

    /// <summary>
    /// Разобрать сертификат и принять подпись, если она выдерживает все проверки.
    /// Порядок проверок — от дешёвых к дорогим и от понятных к техническим: подписант
    /// должен увидеть «истёк сертификат», а не «цепочка не построена», когда верно и то,
    /// и другое.
    /// </summary>
    private async Task<Accepted> AcceptAsync(
        QualifiedSignRequest request, byte[] hash, int userId)
    {
        var certificate = ParseCertificate(request.Certificate);

        try
        {
            var signature = ParseBase64(request.Signature, "подпись");
            var now = DateTime.UtcNow;

            if (now < certificate.NotBefore.ToUniversalTime())
                throw new InvalidOperationException(
                    $"Сертификат вступает в силу {certificate.NotBefore:dd.MM.yyyy} — подписание невозможно");

            if (now > certificate.NotAfter.ToUniversalTime())
                throw new InvalidOperationException(
                    $"Срок действия сертификата истёк {certificate.NotAfter:dd.MM.yyyy}");

            var trust = await _trust.ValidateAsync(certificate);
            if (!trust.Trusted)
                throw new InvalidOperationException(trust.Reason ?? "Сертификат не подтверждён");

            await RequireBelongsToUserAsync(certificate, userId);

            if (!Verify(certificate, hash, signature))
                throw new InvalidOperationException(
                    "Подпись не соответствует хешу подписанного или сертификату подписанта");

            // Метка времени ставится последней: штамповать нечего, пока подпись не
            // признана. Служба меток видит только свёртку подписи, не документ.
            var timestamp = await _timestamps.StampAsync(signature);

            if (!timestamp.Obtained && !timestamp.Disabled)
            {
                var settings = await _db.SigningSettings.AsNoTracking().FirstOrDefaultAsync();

                // Отказ службы меток не всегда должен отменять подпись: приказ иногда
                // нужно подписать именно сейчас. Решает банк, поэтому это настройка.
                if (settings?.TimestampRequired == true)
                    throw new InvalidOperationException(
                        $"Метка времени обязательна, но получить её не удалось: {timestamp.Reason}");

                _logger.LogWarning(
                    "Подпись принята без метки времени: {Reason}", timestamp.Reason);
            }

            return new Accepted(certificate, trust, timestamp);
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    /// <summary>Принятая подпись со всем, что о ней удалось установить.</summary>
    private record Accepted(X509Certificate2 Certificate, TrustResult Trust, TimestampResult Timestamp);

    /// <summary>
    /// Сертификат закрепляется за сотрудником при первой подписи и дальше принимается
    /// только от него. Иначе подпись отвечала бы на вопрос «ключ действующий?», но не
    /// на вопрос «чей он?» — и любой владелец действующего ключа мог бы закрыть чужой этап.
    /// </summary>
    private async Task RequireBelongsToUserAsync(X509Certificate2 certificate, int userId)
    {
        var thumbprint = certificate.Thumbprint;

        var known = await _db.UserCertificates
            .FirstOrDefaultAsync(c => c.Thumbprint == thumbprint);

        if (known is null)
        {
            _db.UserCertificates.Add(new UserCertificate
            {
                UserId = userId,
                Thumbprint = thumbprint,
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                SerialNumber = certificate.SerialNumber,
                NotBefore = certificate.NotBefore.ToUniversalTime(),
                NotAfter = certificate.NotAfter.ToUniversalTime(),
                RawData = certificate.RawData,
                RegisteredAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "КЭП: сертификат {Thumbprint} закреплён за пользователем {UserId}", thumbprint, userId);
            return;
        }

        if (known.UserId != userId)
        {
            var owner = await UserNameAsync(known.UserId);
            throw new InvalidOperationException(
                $"Этот сертификат закреплён за другим сотрудником{(owner is null ? string.Empty : $" ({owner})")} — " +
                "подписывать чужим ключом нельзя");
        }

        if (known.RevokedAt is not null)
            throw new InvalidOperationException(
                $"Сертификат отозван {known.RevokedAt:dd.MM.yyyy}" +
                (string.IsNullOrWhiteSpace(known.RevokedReason) ? string.Empty : $": {known.RevokedReason}"));
    }

    /// <summary>
    /// Проверка подписи хеша. Плагины отдают либо «сырую» подпись хеша, либо
    /// контейнер CMS/PKCS#7 — принимаем оба вида, потому что состав средств ЭП
    /// у банка фиксируется только на этапе обследования.
    /// </summary>
    private static bool Verify(X509Certificate2 certificate, byte[] hash, byte[] signature)
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

            // Цепочку здесь не строим намеренно: её проверяет ICertificateTrustService
            // по корням банка. Встроенная проверка CMS смотрела бы в системное
            // хранилище сервера, то есть доверяла бы не тем, кому доверяет банк.
            cms.CheckSignature(verifySignatureOnly: true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    // ── вспомогательное ──────────────────────────────────────────────────────

    private static string BuildStamp(
        X509Certificate2 certificate, TrustResult trust, TimestampResult timestamp) =>
        JsonSerializer.Serialize(new
        {
            subject = certificate.Subject,
            issuer = certificate.Issuer,
            serial = certificate.SerialNumber,
            validFrom = certificate.NotBefore.ToUniversalTime(),
            validTo = certificate.NotAfter.ToUniversalTime(),
            algorithm = certificate.SignatureAlgorithm.FriendlyName,
            thumbprint = certificate.Thumbprint,
            trustAuthority = trust.AuthorityTitle,
            trustNotChecked = trust.NotChecked,
            revocationChecked = trust.RevocationChecked,
            revocationNote = trust.RevocationNote,
            timestampAt = timestamp.At,
            timestampAuthority = timestamp.Authority,
            timestampNote = timestamp.Obtained ? null : timestamp.Reason,
        });

    /// <summary>
    /// Токен метки хранится целиком: он и есть доказательство. Восстановить его
    /// потом нельзя — запрос к службе не повторяется с тем же результатом.
    /// </summary>
    private async Task SaveTimestampAsync(int signatureId, TimestampResult timestamp)
    {
        if (!timestamp.Obtained) return;

        var signature = await _db.Signatures.FirstOrDefaultAsync(s => s.Id == signatureId);
        if (signature is null) return;

        signature.TimestampToken = timestamp.Token;
        signature.TimestampedAt = timestamp.At;
        signature.TimestampAuthority = timestamp.Authority;

        await _db.SaveChangesAsync();
    }

    private async Task<string?> UserNameAsync(int userId) => await _db.Users
        .Where(u => u.Id == userId)
        .Select(u => u.FullName)
        .FirstOrDefaultAsync();

    private async Task<List<SignatureInfoDto>> LoadSignaturesAsync(
        System.Linq.Expressions.Expression<Func<Signature, bool>> filter)
    {
        var rows = await _db.Signatures
            .Where(filter)
            .OrderBy(s => s.At)
            .ToListAsync();

        var userIds = rows.Select(s => s.UserId).Distinct().ToList();
        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return rows.Select(s => ToDto(s, names.GetValueOrDefault(s.UserId), null, null, null)).ToList();
    }

    private static SignatureInfoDto ToDto(
        Signature s, string? userName, X509Certificate2? certificate,
        TrustResult? trust, TimestampResult? timestamp)
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
            dto.TrustAuthority = trust?.AuthorityTitle;
            dto.TrustNotChecked = trust?.NotChecked ?? false;
            dto.RevocationChecked = trust?.RevocationChecked ?? false;
            dto.RevocationNote = trust?.RevocationNote;
            dto.TimestampedAt = timestamp?.At;
            dto.TimestampAuthority = timestamp?.Authority;
            dto.TimestampNote = timestamp is null || timestamp.Obtained ? null : timestamp.Reason;
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
            if (root.TryGetProperty("trustAuthority", out var authority))
                dto.TrustAuthority = authority.GetString();
            if (root.TryGetProperty("trustNotChecked", out var notChecked) &&
                notChecked.ValueKind is JsonValueKind.True or JsonValueKind.False)
                dto.TrustNotChecked = notChecked.GetBoolean();
            if (root.TryGetProperty("revocationChecked", out var revocation) &&
                revocation.ValueKind is JsonValueKind.True or JsonValueKind.False)
                dto.RevocationChecked = revocation.GetBoolean();
            if (root.TryGetProperty("revocationNote", out var revocationNote))
                dto.RevocationNote = revocationNote.GetString();
            if (root.TryGetProperty("timestampAuthority", out var tsa))
                dto.TimestampAuthority = tsa.GetString();
            if (root.TryGetProperty("timestampNote", out var tsNote))
                dto.TimestampNote = tsNote.GetString();

            // Время метки берём из самой подписи, а не из штампа: в базе оно
            // типизировано, и разбирать его из текста незачем.
            dto.TimestampedAt = s.TimestampedAt;
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

    /// <summary>
    /// Отпечаток карточки хранится в base64 — для подписи он нужен байтами.
    /// </summary>
    private static byte[] FingerprintToBytes(string fingerprint)
    {
        try
        {
            return Convert.FromBase64String(fingerprint);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Отпечаток карточки записан в неожиданном виде — подписание невозможно");
        }
    }
}
