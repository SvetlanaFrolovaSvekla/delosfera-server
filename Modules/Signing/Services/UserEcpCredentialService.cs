using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Signing.Models.InfocomEcp;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Машиночитаемые коды ошибок сохранённой ЭЦП-идентификации — по ним клиент решает, открыть ли
/// обычное окно с вводом ПИН и кода.</summary>
public static class EcpCredentialErrorCodes
{
    /// <summary>Идентификация не проходилась (или отозвана) — нужен обычный флоу.</summary>
    public const string Missing = "ecp_credential_missing";
    /// <summary>Срок сохранённой идентификации истёк.</summary>
    public const string Expired = "ecp_credential_expired";
    /// <summary>ГП не принял сохранённый код (истёк/сменился) — запись удалена, нужна новая идентификация.</summary>
    public const string Invalid = "ecp_credential_invalid";
    /// <summary>На сервере не задан ключ шифрования (EcpCredentials:EncryptionKey).</summary>
    public const string NotConfigured = "ecp_credential_not_configured";
    public const string ProviderUnavailable = "provider_unavailable";
}

public class EcpCredentialException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public class EcpCredentialStatusResponse
{
    /// <summary>true — на сервере задан ключ шифрования, идентификацию можно сохранять.</summary>
    public bool Available { get; set; }
    public bool Enrolled { get; set; }
    /// <summary>ПИН с маскировкой (первые 2 и последние 2 цифры).</summary>
    public string? PersonIdnpMasked { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int RetentionDays { get; set; }
}

/// <summary>Результат подписания хэша резолюции сохранённой идентификацией — всё, что клиент присылает
/// дальше в POST .../decision (EcpSignatureBase64/EcpHash/EcpPersonIdnp/EcpTimestamp/EcpCertToken).</summary>
public class StoredEcpSignatureResponse
{
    public string SignatureBase64 { get; set; } = "";
    public string Hash { get; set; } = "";
    public string PersonIdnp { get; set; } = "";
    public long Timestamp { get; set; }
    /// <summary>Токен из 2.3 — нужен только для best-effort 2.4 (сведения о сертификате в журнал) при
    /// сохранении решения, нигде не хранится (тот же принцип, что и при обычном флоу).</summary>
    public string CertToken { get; set; } = "";
}

public interface IUserEcpCredentialService
{
    Task<EcpCredentialStatusResponse> GetStatusAsync(int userId, CancellationToken ct = default);

    /// <summary>Проверяет код обменом на токен (2.3) и, если ГП его принял, сохраняет ПИН и код
    /// зашифрованно на срок RetentionDays.</summary>
    Task<EcpCredentialStatusResponse> SaveAsync(int userId, string personIdnp, string code, CancellationToken ct = default);

    Task RevokeAsync(int userId, CancellationToken ct = default);

    /// <summary>Подписывает хэш резолюции (его строит backend, см. ResolutionSignatureService) сохранённой
    /// идентификацией: обмен кода на минутный токен (2.3) → 2.5 get-sign/for-hash.</summary>
    Task<StoredEcpSignatureResponse> SignResolutionAsync(
        int userId, int vndId, int stageId, string? comment, CancellationToken ct = default);
}

public sealed class UserEcpCredentialService : IUserEcpCredentialService
{
    private readonly DelosferaDbContext _db;
    private readonly IInfocomEcpClient _infocom;
    private readonly IEcpCredentialProtector _protector;
    private readonly IResolutionSignatureService _resolutionSignatures;
    private readonly EcpCredentialOptions _options;
    private readonly ILogger<UserEcpCredentialService> _logger;

    public UserEcpCredentialService(
        DelosferaDbContext db, IInfocomEcpClient infocom, IEcpCredentialProtector protector,
        IResolutionSignatureService resolutionSignatures, IOptions<EcpCredentialOptions> options,
        ILogger<UserEcpCredentialService> logger)
    {
        _db = db;
        _infocom = infocom;
        _protector = protector;
        _resolutionSignatures = resolutionSignatures;
        _options = options.Value;
        _logger = logger;
    }

    private int RetentionDays => Math.Clamp(_options.RetentionDays, 1, 365);

    public async Task<EcpCredentialStatusResponse> GetStatusAsync(int userId, CancellationToken ct = default)
    {
        var status = new EcpCredentialStatusResponse { Available = _protector.IsConfigured, RetentionDays = RetentionDays };
        var cred = await _db.UserEcpCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cred is null || cred.ExpiresAt <= DateTime.UtcNow) return status;

        status.Enrolled = true;
        status.PersonIdnpMasked = Mask(cred.PersonIdnp);
        status.CreatedAt = cred.CreatedAt;
        status.ExpiresAt = cred.ExpiresAt;
        status.LastUsedAt = cred.LastUsedAt;
        return status;
    }

    public async Task<EcpCredentialStatusResponse> SaveAsync(
        int userId, string personIdnp, string code, CancellationToken ct = default)
    {
        EnsureConfigured();
        code = code.Trim();

        // Код должен быть принят ГП до сохранения: иначе пользователь узнает об ошибке только при
        // первом подписании. Токен из ответа не нужен и не сохраняется.
        await ExchangeAsync(personIdnp, code, ct);

        var now = DateTime.UtcNow;
        var cred = await _db.UserEcpCredentials.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cred is null)
        {
            cred = new UserEcpCredential { UserId = userId };
            _db.UserEcpCredentials.Add(cred);
        }

        cred.PersonIdnp = personIdnp;
        cred.EncryptedCode = _protector.Protect(userId, personIdnp, code);
        cred.CreatedAt = now;
        cred.ExpiresAt = now.AddDays(RetentionDays);
        cred.LastUsedAt = null;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("ЭЦП-идентификация сохранена: userId={UserId}, до {ExpiresAt:O}", userId, cred.ExpiresAt);
        return await GetStatusAsync(userId, ct);
    }

    public async Task RevokeAsync(int userId, CancellationToken ct = default)
    {
        var cred = await _db.UserEcpCredentials.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cred is null) return;
        _db.UserEcpCredentials.Remove(cred);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("ЭЦП-идентификация отозвана: userId={UserId}", userId);
    }

    public async Task<StoredEcpSignatureResponse> SignResolutionAsync(
        int userId, int vndId, int stageId, string? comment, CancellationToken ct = default)
    {
        EnsureConfigured();

        var cred = await _db.UserEcpCredentials.FirstOrDefaultAsync(c => c.UserId == userId, ct)
                   ?? throw new EcpCredentialException(StatusCodes.Status409Conflict, EcpCredentialErrorCodes.Missing,
                       "Электронная подпись не подключена — пройдите идентификацию");

        if (cred.ExpiresAt <= DateTime.UtcNow)
        {
            _db.UserEcpCredentials.Remove(cred);
            await _db.SaveChangesAsync(ct);
            throw new EcpCredentialException(StatusCodes.Status409Conflict, EcpCredentialErrorCodes.Expired,
                "Срок действия идентификации истёк — пройдите её заново");
        }

        var code = _protector.Unprotect(userId, cred.PersonIdnp, cred.EncryptedCode);
        if (code is null)
        {
            // Ключ сменили или запись повреждена — расшифровать нельзя, запись бесполезна.
            _logger.LogWarning("ЭЦП-идентификация не расшифровывается (смена ключа?): userId={UserId}", userId);
            _db.UserEcpCredentials.Remove(cred);
            await _db.SaveChangesAsync(ct);
            throw new EcpCredentialException(StatusCodes.Status409Conflict, EcpCredentialErrorCodes.Invalid,
                "Сохранённая идентификация недействительна — пройдите её заново");
        }

        // Хэш строит backend (текст резолюции + содержимое редакции + код ВНД) и заодно проверяет,
        // что пользователь — назначенный согласующий этого этапа на активной фазе. Делаем это ДО
        // обмена кода на токен, чтобы не тратить обращения к ГП зря.
        var hashResponse = await _resolutionSignatures.GetHashForStageAsync(vndId, stageId, comment, userId, ct);

        string token;
        try
        {
            token = await ExchangeAsync(cred.PersonIdnp, code, ct);
        }
        catch (EcpCredentialException ex) when (ex.Code == EcpCredentialErrorCodes.Invalid)
        {
            // ГП не принял сохранённый код — не повторяем его (у ГП есть счётчик неверных попыток)
            // и удаляем запись, чтобы пользователь прошёл идентификацию заново.
            _db.UserEcpCredentials.Remove(cred);
            await _db.SaveChangesAsync(ct);
            throw;
        }

        JsonElement signJson;
        try
        {
            signJson = await _infocom.SignHashAsync(new InfocomSignHashRequest { Hash = hashResponse.Hash, UserToken = token }, ct);
        }
        catch (InfocomEcpException ex)
        {
            throw new EcpCredentialException(ex.StatusCode is >= 400 and < 500 ? ex.StatusCode : StatusCodes.Status502BadGateway,
                EcpCredentialErrorCodes.ProviderUnavailable, ex.Message);
        }

        var ok = signJson.TryGetProperty("isSuccesfull", out var okEl) && okEl.ValueKind == JsonValueKind.True;
        var sign = signJson.TryGetProperty("sign", out var signEl) && signEl.ValueKind == JsonValueKind.String
            ? signEl.GetString() : null;
        if (!ok || string.IsNullOrEmpty(sign))
            throw new EcpCredentialException(StatusCodes.Status502BadGateway, EcpCredentialErrorCodes.ProviderUnavailable,
                "Подпись не получена");
        var timestamp = signJson.TryGetProperty("timestamp", out var tsEl) && tsEl.TryGetInt64(out var ts) ? ts : 0L;

        cred.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new StoredEcpSignatureResponse
        {
            SignatureBase64 = sign,
            Hash = hashResponse.Hash,
            PersonIdnp = cred.PersonIdnp,
            Timestamp = timestamp,
            CertToken = token,
        };
    }

    /// <summary>2.3 account/auth: обмен кода на минутный токен. 4xx от ГП ("Неверный ПИН-код! Осталось
    /// попыток: N", учётка не активна и т.п.) → <see cref="EcpCredentialErrorCodes.Invalid"/> с текстом
    /// провайдера; сетевые сбои и 5xx → 502/504.</summary>
    private async Task<string> ExchangeAsync(string personIdnp, string code, CancellationToken ct)
    {
        JsonElement json;
        try
        {
            json = await _infocom.AccountAuthAsync(new InfocomAccountAuthRequest { PersonIdnp = personIdnp, ByPin = code }, ct);
        }
        catch (InfocomEcpException ex) when (ex.StatusCode is >= 400 and < 500)
        {
            throw new EcpCredentialException(StatusCodes.Status400BadRequest, EcpCredentialErrorCodes.Invalid, ex.Message);
        }
        catch (InfocomEcpException ex)
        {
            throw new EcpCredentialException(StatusCodes.Status502BadGateway, EcpCredentialErrorCodes.ProviderUnavailable, ex.Message);
        }
        catch (HttpRequestException)
        {
            throw new EcpCredentialException(StatusCodes.Status502BadGateway, EcpCredentialErrorCodes.ProviderUnavailable,
                "Нет связи с сервисом ЭЦП");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EcpCredentialException(StatusCodes.Status504GatewayTimeout, EcpCredentialErrorCodes.ProviderUnavailable,
                "Таймаут сервиса ЭЦП");
        }

        var token = json.TryGetProperty("token", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.String
            ? tokenEl.GetString() : null;
        if (string.IsNullOrEmpty(token))
            throw new EcpCredentialException(StatusCodes.Status400BadRequest, EcpCredentialErrorCodes.Invalid,
                "Токен доступа не получен");

        // Токен выдан на ПИН из ответа — он должен совпасть с введённым (защита от опечатки/подмены).
        if (json.TryGetProperty("personIdnp", out var pinEl) && pinEl.ValueKind == JsonValueKind.Number
            && pinEl.TryGetInt64(out var pinNumber)
            && pinNumber.ToString().PadLeft(14, '0') != personIdnp)
            throw new EcpCredentialException(StatusCodes.Status400BadRequest, EcpCredentialErrorCodes.Invalid,
                "ПИН владельца подписи не совпадает с введённым");

        return token;
    }

    private void EnsureConfigured()
    {
        if (!_protector.IsConfigured)
            throw new EcpCredentialException(StatusCodes.Status503ServiceUnavailable, EcpCredentialErrorCodes.NotConfigured,
                "Сохранение электронной подписи не настроено на сервере");
    }

    private static string Mask(string pin) =>
        pin.Length == 14 ? $"{pin[..2]}••••••••••{pin[^2..]}" : "••••";
}
