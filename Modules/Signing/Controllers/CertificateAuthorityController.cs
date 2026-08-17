using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Authorization;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Signing.Controllers;

public class AddAuthorityRequest
{
    /// <summary>Сертификат в DER или PEM, base64.</summary>
    public required string Certificate { get; set; }

    /// <summary>Как называть центр в интерфейсе. Пусто — возьмём из самого сертификата.</summary>
    public string? Title { get; set; }
}

public class DisableAuthorityRequest
{
    public string? Reason { get; set; }
}

public class RevokeCertificateRequest
{
    public string? Reason { get; set; }
}

/// <summary>
/// Удостоверяющие центры, которым доверяет банк, и сертификаты сотрудников (SIG-02).
///
/// Пока список центров пуст, подпись принимается без проверки цепочки и помечается
/// как непроверенная. Заведение первого корня включает проверку — поэтому здесь же
/// видно, сколько сертификатов уже закреплено за людьми: после включения проверки
/// часть из них может перестать подходить, и лучше узнать об этом до, а не в момент
/// подписания приказа.
/// </summary>
[ApiController]
[Authorize]
[Route("api/signing/authorities")]
[Tags("Подписание")]
public class CertificateAuthorityController : ControllerBase
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public CertificateAuthorityController(
        DelosferaDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    /// <summary>Список доверенных центров с признаком, включена ли проверка вообще.</summary>
    [HttpGet]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var authorities = await _db.TrustedCertificateAuthorities
            .AsNoTracking()
            .OrderByDescending(a => a.IsActive)
            .ThenBy(a => a.Title)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Subject,
                a.Issuer,
                a.Thumbprint,
                a.SerialNumber,
                a.NotBefore,
                a.NotAfter,
                a.IsActive,
                a.DisabledReason,
                a.AddedAt,
                selfSigned = a.Subject == a.Issuer,
                expired = a.NotAfter < DateTime.UtcNow,
            })
            .ToListAsync(ct);

        return Ok(new
        {
            items = authorities,
            trustEnforced = authorities.Any(a => a.IsActive),
            registeredCertificates = await _db.UserCertificates.CountAsync(c => c.RevokedAt == null, ct),
        });
    }

    /// <summary>Завести корневой или промежуточный сертификат удостоверяющего центра.</summary>
    [HttpPost]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Add([FromBody] AddAuthorityRequest request, CancellationToken ct)
    {
        X509Certificate2 certificate;

        try
        {
            certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(Strip(request.Certificate)));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return BadRequest(new {message = "Файл не разобран как сертификат: ожидается DER или PEM"});
        }

        using (certificate)
        {
            if (await _db.TrustedCertificateAuthorities.AnyAsync(a => a.Thumbprint == certificate.Thumbprint, ct))
                return BadRequest(new {message = "Этот сертификат уже заведён"});

            // Сертификат сотрудника в списке доверенных центров означал бы, что доверяют
            // не центру, а одному человеку: цепочка от него никуда не ведёт.
            if (certificate.Extensions.OfType<X509BasicConstraintsExtension>()
                    .FirstOrDefault() is {CertificateAuthority: false})
                return BadRequest(new
                {
                    message = "Это сертификат конечного владельца, а не удостоверяющего центра — " +
                              "цепочку по нему построить нельзя",
                });

            var authority = new TrustedCertificateAuthority
            {
                Title = string.IsNullOrWhiteSpace(request.Title)
                    ? ShortName(certificate.Subject)
                    : request.Title.Trim(),
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                Thumbprint = certificate.Thumbprint,
                SerialNumber = certificate.SerialNumber,
                NotBefore = certificate.NotBefore.ToUniversalTime(),
                NotAfter = certificate.NotAfter.ToUniversalTime(),
                RawData = certificate.RawData,
                IsActive = true,
                AddedByUserId = _currentUser.UserId,
                AddedAt = DateTime.UtcNow,
            };

            _db.TrustedCertificateAuthorities.Add(authority);
            await _db.SaveChangesAsync(ct);

            // Кому доверяет банк — вопрос того же порядка, что и права доступа:
            // должно быть видно, кто расширил круг и когда.
            await _audit.LogAsync("TrustedCertificateAuthority", authority.Id, "Added", _currentUser.UserId, new
            {
                authority.Title,
                authority.Subject,
                authority.Thumbprint,
                authority.NotAfter,
            });

            return Ok(new {authority.Id, authority.Title, authority.Thumbprint, authority.NotAfter});
        }
    }

    /// <summary>Снять доверие. Запись остаётся: прежние подписи должны остаться объяснимыми.</summary>
    [HttpPost("{id:int}/disable")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Disable(
        int id, [FromBody] DisableAuthorityRequest request, CancellationToken ct)
    {
        var authority = await _db.TrustedCertificateAuthorities.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (authority is null) return NotFound(new {message = "Удостоверяющий центр не найден"});

        authority.IsActive = false;
        authority.DisabledAt = DateTime.UtcNow;
        authority.DisabledReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("TrustedCertificateAuthority", authority.Id, "Disabled", _currentUser.UserId,
            new {authority.Title, authority.DisabledReason});

        return Ok(new {authority.Id, authority.IsActive});
    }

    /// <summary>Вернуть доверие снятому центру.</summary>
    [HttpPost("{id:int}/enable")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Enable(int id, CancellationToken ct)
    {
        var authority = await _db.TrustedCertificateAuthorities.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (authority is null) return NotFound(new {message = "Удостоверяющий центр не найден"});

        authority.IsActive = true;
        authority.DisabledAt = null;
        authority.DisabledReason = null;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("TrustedCertificateAuthority", authority.Id, "Enabled", _currentUser.UserId,
            new {authority.Title});

        return Ok(new {authority.Id, authority.IsActive});
    }

    /// <summary>Сертификаты сотрудников: кто чем подписывает.</summary>
    [HttpGet("/api/signing/certificates")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> Certificates(CancellationToken ct) =>
        Ok(await _db.UserCertificates
            .AsNoTracking()
            .OrderByDescending(c => c.RegisteredAt)
            .Select(c => new
            {
                c.Id,
                c.UserId,
                userName = _db.Users.Where(u => u.Id == c.UserId).Select(u => u.FullName).FirstOrDefault(),
                c.Subject,
                c.Issuer,
                c.SerialNumber,
                c.NotBefore,
                c.NotAfter,
                c.RegisteredAt,
                c.RevokedAt,
                c.RevokedReason,
                expired = c.NotAfter < DateTime.UtcNow,
            })
            .ToListAsync(ct));

    /// <summary>Свой сертификат — чтобы подписант видел, чем он подписывает и до какого числа.</summary>
    [HttpGet("/api/signing/certificates/mine")]
    public async Task<IActionResult> MyCertificates(CancellationToken ct) =>
        Ok(await _db.UserCertificates
            .AsNoTracking()
            .Where(c => c.UserId == _currentUser.UserId)
            .OrderByDescending(c => c.RegisteredAt)
            .Select(c => new
            {
                c.Id,
                c.Subject,
                c.Issuer,
                c.SerialNumber,
                c.NotBefore,
                c.NotAfter,
                c.RegisteredAt,
                c.RevokedAt,
                c.RevokedReason,
                expired = c.NotAfter < DateTime.UtcNow,
            })
            .ToListAsync(ct));

    /// <summary>
    /// Отозвать сертификат сотрудника: увольнение, компрометация ключа, замена по сроку.
    /// Подписи, поставленные до отзыва, остаются действительными — отзыв смотрит вперёд.
    /// </summary>
    [HttpPost("/api/signing/certificates/{id:int}/revoke")]
    [RequirePermission(PermissionCode.ManageSystemSettings)]
    public async Task<IActionResult> RevokeCertificate(
        int id, [FromBody] RevokeCertificateRequest request, CancellationToken ct)
    {
        var certificate = await _db.UserCertificates.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (certificate is null) return NotFound(new {message = "Сертификат не найден"});

        certificate.RevokedAt = DateTime.UtcNow;
        certificate.RevokedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("UserCertificate", certificate.Id, "Revoked", _currentUser.UserId, new
        {
            certificate.UserId,
            certificate.Subject,
            certificate.RevokedReason,
        });

        return Ok(new {certificate.Id, certificate.RevokedAt});
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private static string Strip(string raw) => raw
        .Replace("-----BEGIN CERTIFICATE-----", string.Empty)
        .Replace("-----END CERTIFICATE-----", string.Empty)
        .Replace("\r", string.Empty)
        .Replace("\n", string.Empty)
        .Trim();

    /// <summary>
    /// Из «CN=Infocom Root CA, O=…, C=KG» оставить «Infocom Root CA»: администратору
    /// нужно название центра, а не разобранное имя целиком.
    /// </summary>
    private static string ShortName(string subject)
    {
        var cn = subject
            .Split(',', StringSplitOptions.TrimEntries)
            .FirstOrDefault(part => part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase));

        return cn is null ? subject : cn[3..].Trim();
    }
}
