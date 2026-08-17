using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Итог проверки сертификата: доверяем или нет и почему.</summary>
public class TrustResult
{
    public bool Trusted { get; init; }

    /// <summary>Почему не доверяем — текст показывается подписанту дословно.</summary>
    public string? Reason { get; init; }

    /// <summary>Через какой удостоверяющий центр выстроилась цепочка.</summary>
    public string? AuthorityTitle { get; init; }

    /// <summary>
    /// Проверка не выполнялась: список доверенных центров пуст. Подпись принимается,
    /// но в штампе это отмечено — иначе она выглядела бы проверенной.
    /// </summary>
    public bool NotChecked { get; init; }

    public static TrustResult Ok(string? authority) => new() {Trusted = true, AuthorityTitle = authority};
    public static TrustResult Fail(string reason) => new() {Trusted = false, Reason = reason};
    public static TrustResult Skipped() => new() {Trusted = true, NotChecked = true};
}

public interface ICertificateTrustService
{
    /// <summary>Построить цепочку сертификата до доверенного корня банка.</summary>
    Task<TrustResult> ValidateAsync(X509Certificate2 certificate, CancellationToken ct = default);
}

/// <summary>
/// Проверка цепочки доверия сертификата подписанта (SIG-02).
///
/// Цепочка строится только по корням, которые банк завёл сам: системное хранилище
/// сервера намеренно не используется. Иначе доверие определялось бы содержимым
/// образа контейнера, а не решением банка, и любой публичный УЦ мира подписывал бы
/// документы Правления наравне с «Инфокомом».
///
/// Пока ни одного корня не заведено, проверка пропускается, а подпись помечается как
/// непроверенную по цепочке. Отказывать было бы честнее, но тогда до заведения
/// корней нельзя было бы подписать ничего вообще — включая заведение самих корней.
/// Молча принимать за проверенную — нельзя: об этом сказано в штампе.
/// </summary>
public class CertificateTrustService : ICertificateTrustService
{
    private readonly DelosferaDbContext _db;
    private readonly ILogger<CertificateTrustService> _logger;

    public CertificateTrustService(DelosferaDbContext db, ILogger<CertificateTrustService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TrustResult> ValidateAsync(X509Certificate2 certificate, CancellationToken ct = default)
    {
        var authorities = await _db.TrustedCertificateAuthorities
            .AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync(ct);

        if (authorities.Count == 0)
        {
            _logger.LogWarning(
                "Проверка цепочки пропущена: доверенные удостоверяющие центры не заведены");
            return TrustResult.Skipped();
        }

        using var chain = new X509Chain();
        var policy = chain.ChainPolicy;

        policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        policy.RevocationMode = X509RevocationMode.NoCheck;
        policy.VerificationFlags = X509VerificationFlags.NoFlag;

        // Промежуточные и корневые различаются тем, совпадают ли Subject и Issuer:
        // корень подписан сам собой. Класть промежуточный в доверенные корни нельзя —
        // .NET требует, чтобы цепочка замыкалась именно самоподписанным.
        var loaded = new List<X509Certificate2>();

        try
        {
            foreach (var authority in authorities)
            {
                var loadedCertificate = X509CertificateLoader.LoadCertificate(authority.RawData);
                loaded.Add(loadedCertificate);

                if (IsSelfSigned(loadedCertificate))
                    policy.CustomTrustStore.Add(loadedCertificate);
                else
                    policy.ExtraStore.Add(loadedCertificate);
            }

            if (policy.CustomTrustStore.Count == 0)
                return TrustResult.Fail(
                    "Заведены только промежуточные сертификаты — цепочку не на что замкнуть. " +
                    "Добавьте корневой сертификат удостоверяющего центра");

            if (chain.Build(certificate))
            {
                var root = chain.ChainElements[^1].Certificate;
                var authority = authorities.FirstOrDefault(a => a.Thumbprint == root.Thumbprint);
                return TrustResult.Ok(authority?.Title ?? root.Subject);
            }

            var problems = chain.ChainStatus
                .Where(s => s.Status != X509ChainStatusFlags.NoError)
                .Select(Explain)
                .Distinct()
                .ToList();

            return TrustResult.Fail(problems.Count > 0
                ? $"Сертификат не подтверждён удостоверяющим центром банка: {string.Join("; ", problems)}"
                : "Сертификат не подтверждён удостоверяющим центром банка");
        }
        finally
        {
            foreach (var certificateToDispose in loaded) certificateToDispose.Dispose();
        }
    }

    private static bool IsSelfSigned(X509Certificate2 certificate) =>
        certificate.SubjectName.RawData.SequenceEqual(certificate.IssuerName.RawData);

    /// <summary>
    /// Причина отказа своими словами. Подписант читает её в момент, когда подписать не
    /// получилось, и должен понять, идти ему к администратору или в удостоверяющий центр.
    /// </summary>
    private static string Explain(X509ChainStatus status) => status.Status switch
    {
        X509ChainStatusFlags.UntrustedRoot or X509ChainStatusFlags.PartialChain =>
            "цепочка не доходит до корня, которому доверяет банк",
        X509ChainStatusFlags.NotTimeValid =>
            "истёк срок действия сертификата в цепочке",
        X509ChainStatusFlags.NotSignatureValid =>
            "подпись удостоверяющего центра на сертификате не сходится",
        X509ChainStatusFlags.NotValidForUsage =>
            "сертификат выдан не для подписания документов",
        X509ChainStatusFlags.Revoked =>
            "сертификат отозван удостоверяющим центром",
        X509ChainStatusFlags.RevocationStatusUnknown or X509ChainStatusFlags.OfflineRevocation =>
            "не удалось проверить, не отозван ли сертификат",
        _ => status.StatusInformation.Trim() is {Length: > 0} text
            ? text.ToLowerInvariant()
            : "цепочка не построена",
    };
}
