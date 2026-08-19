using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>
/// Перепроверка сертификатов сотрудников на отзыв (Б-18).
///
/// Проверка в момент подписания отвечает на вопрос «можно ли подписать сейчас».
/// Но сертификат отзывают между подписаниями — при увольнении, при компрометации
/// ключа, — и узнать об этом система должна раньше, чем человек снова придёт
/// подписывать приказ. Иначе отозванный ключ продолжал бы работать до того дня,
/// когда до него дойдёт очередь.
///
/// Отзыв, найденный здесь, смотрит вперёд: подписи, поставленные до него, остаются
/// действительными. Метка времени как раз для этого и нужна — без неё нельзя было
/// бы отличить подпись, поставленную до отзыва, от поставленной после.
///
/// Просроченные сертификаты не проверяются: истёк срок — и без списка отзыва ясно,
/// что подписывать им нельзя.
/// </summary>
public class CertificateRevocationWorker : BackgroundService
{
    /// <summary>
    /// Как часто просыпаться. Сама частота перепроверки задаётся в настройках; здесь
    /// лишь шаг опроса, достаточно редкий, чтобы не дёргать службы отзыва зря.
    /// </summary>
    private static readonly TimeSpan Step = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CertificateRevocationWorker> _logger;

    public CertificateRevocationWorker(
        IServiceScopeFactory scopeFactory, ILogger<CertificateRevocationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ПройтиАsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Перепроверка сертификатов на отзыв: сбой обхода");
            }

            await Task.Delay(Step, stoppingToken);
        }
    }

    private async Task ПройтиАsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var settings = await db.SigningSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null || !settings.RevocationCheckEnabled) return;

        var порог = DateTime.UtcNow.AddHours(-Math.Max(1, settings.RevocationRecheckHours));

        var сертификаты = await db.UserCertificates
            .Where(c => c.RevokedAt == null
                        && c.NotAfter > DateTime.UtcNow
                        && (c.RevocationCheckedAt == null || c.RevocationCheckedAt < порог))
            .ToListAsync(ct);

        if (сертификаты.Count == 0) return;

        var корни = await db.TrustedCertificateAuthorities
            .AsNoTracking()
            .Where(a => a.IsActive)
            .Select(a => a.RawData)
            .ToListAsync(ct);

        foreach (var запись in сертификаты)
        {
            if (ct.IsCancellationRequested) return;

            var (отозван, проверено) = Проверить(запись.RawData, корни);

            запись.RevocationCheckedAt = DateTime.UtcNow;

            if (!проверено || !отозван) continue;

            запись.RevokedAt = DateTime.UtcNow;
            запись.RevokedReason = "Отозван удостоверяющим центром";

            _logger.LogWarning(
                "Сертификат {Thumbprint} пользователя {UserId} отозван удостоверяющим центром",
                запись.Thumbprint, запись.UserId);

            // Отзыв сертификата — событие того же порядка, что блокировка учётной
            // записи: подписывать этот человек больше не может, и это должно быть видно.
            await audit.LogAsync("UserCertificate", запись.Id, "RevokedByAuthority", null, new
            {
                запись.UserId,
                запись.Subject,
                запись.Thumbprint,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Отозван ли сертификат. Различаем «отозван» и «не удалось проверить»: во втором
    /// случае трогать запись нельзя — недоступность службы не повод лишать человека
    /// права подписи.
    /// </summary>
    private static (bool Revoked, bool Checked) Проверить(byte[] raw, List<byte[]> корни)
    {
        using var certificate = X509CertificateLoader.LoadCertificate(raw);
        using var chain = new X509Chain();

        var policy = chain.ChainPolicy;
        policy.RevocationMode = X509RevocationMode.Online;
        policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        policy.UrlRetrievalTimeout = TimeSpan.FromSeconds(20);

        // Нас интересует только отзыв, поэтому всё остальное прощаем: истёкший
        // промежуточный сертификат — отдельный разговор, а не признак отзыва.
        policy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid
                                   | X509VerificationFlags.IgnoreCtlNotTimeValid
                                   | X509VerificationFlags.IgnoreWrongUsage;

        var loaded = new List<X509Certificate2>();

        try
        {
            if (корни.Count > 0)
            {
                policy.TrustMode = X509ChainTrustMode.CustomRootTrust;

                foreach (var сырой in корни)
                {
                    var корень = X509CertificateLoader.LoadCertificate(сырой);
                    loaded.Add(корень);

                    if (корень.SubjectName.RawData.SequenceEqual(корень.IssuerName.RawData))
                        policy.CustomTrustStore.Add(корень);
                    else
                        policy.ExtraStore.Add(корень);
                }
            }

            chain.Build(certificate);

            var статусы = chain.ChainStatus;

            if (статусы.Any(s => s.Status == X509ChainStatusFlags.Revoked)) return (true, true);

            var неизвестно = статусы.Any(s =>
                s.Status is X509ChainStatusFlags.RevocationStatusUnknown
                    or X509ChainStatusFlags.OfflineRevocation
                    or X509ChainStatusFlags.PartialChain
                    or X509ChainStatusFlags.UntrustedRoot);

            return (false, !неизвестно);
        }
        finally
        {
            foreach (var сертификат in loaded) сертификат.Dispose();
        }
    }
}
