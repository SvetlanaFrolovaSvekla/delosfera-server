using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Настройки локальной подписи (печати) системы «Делосфера» — см. appsettings.json → SystemSeal.</summary>
public class SystemSealOptions
{
    public const string Section = "SystemSeal";

    /// <summary>Ключ печати — base64 от 32 и более байт (HMAC-SHA256). НЕ хранить в appsettings.json:
    /// задаётся переменной окружения SystemSeal__HmacKey (прод/тест) или user-secrets (dev).
    /// Сгенерировать: openssl rand -base64 32. Пустой/некорректный ключ — подписание системной
    /// печатью выключено (пользователи с правом SignWithoutNationalEcp получат отказ).
    /// ВАЖНО: ключ нельзя терять и менять без сохранения старого — уже поставленные печати
    /// перестанут проходить повторную проверку. Старые ключи перечисляются в PreviousHmacKeys.</summary>
    public string HmacKey { get; set; } = "";

    /// <summary>Прежние ключи (после ротации) — только для повторной проверки старых печатей.
    /// Новые печати ставятся всегда текущим HmacKey.</summary>
    public List<string> PreviousHmacKeys { get; set; } = [];
}

/// <summary>Локальная подпись (печать) системы «Делосфера» для пользователей без ЭЦП КР
/// (право SignWithoutNationalEcp). Это не ЭЦП: сервер удостоверяет, что резолюцию с данным хэшем
/// вынес именно этот авторизованный пользователь системы в указанный момент, и любая правка
/// записи журнала (хэш, пользователь, этап, решение, время) ломает печать.</summary>
public interface ISystemSealService
{
    /// <summary>true — ключ задан и корректен, печать можно ставить.</summary>
    bool IsConfigured { get; }

    /// <summary>Идентификатор текущего ключа: первые 16 hex SHA-256 ключа (сам ключ не раскрывает).
    /// Сохраняется в журнале, чтобы при ротации было видно, каким ключом поставлена печать.</summary>
    string? KeyId { get; }

    /// <summary>Печать (HMAC-SHA256, base64) над канонической строкой <paramref name="payload"/>.</summary>
    string Seal(string payload);

    /// <summary>true — печать поставлена одним из известных ключей (текущим или прежним) над этим
    /// payload; false — не совпала; null — ключ не настроен, проверить нечем.</summary>
    bool? Verify(string payload, string sealBase64);
}

public sealed class SystemSealService : ISystemSealService
{
    private readonly byte[]? _key;
    private readonly List<byte[]> _verifyKeys = [];

    public SystemSealService(IOptions<SystemSealOptions> options, ILogger<SystemSealService> logger)
    {
        _key = ParseKey(options.Value.HmacKey, "SystemSeal:HmacKey", logger);
        if (_key is null)
            logger.LogInformation("SystemSeal:HmacKey не задан — подписание системной печатью «Делосфера» выключено");
        else
            _verifyKeys.Add(_key);

        foreach (var raw in options.Value.PreviousHmacKeys ?? [])
        {
            var old = ParseKey(raw, "SystemSeal:PreviousHmacKeys", logger);
            if (old is not null) _verifyKeys.Add(old);
        }

        KeyId = _key is null ? null : KeyIdOf(_key);
    }

    public bool IsConfigured => _key is not null;

    public string? KeyId { get; }

    public string Seal(string payload)
    {
        if (_key is null)
            throw new InvalidOperationException("Ключ системной подписи «Делосфера» не задан (SystemSeal:HmacKey)");
        return Convert.ToBase64String(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));
    }

    public bool? Verify(string payload, string sealBase64)
    {
        if (_verifyKeys.Count == 0) return null;

        byte[] seal;
        try
        {
            seal = Convert.FromBase64String(sealBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        var data = Encoding.UTF8.GetBytes(payload);
        foreach (var key in _verifyKeys)
        {
            if (CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, data), seal))
                return true;
        }
        return false;
    }

    private static string KeyIdOf(byte[] key) => Convert.ToHexString(SHA256.HashData(key))[..16];

    private static byte[]? ParseKey(string? raw, string name, ILogger logger)
    {
        raw = raw?.Trim();
        if (string.IsNullOrEmpty(raw)) return null;
        try
        {
            var key = Convert.FromBase64String(raw);
            if (key.Length >= 32) return key;
            logger.LogWarning("{Name} должен быть base64 от 32 и более байт (сейчас {Length}) — ключ не используется",
                name, key.Length);
        }
        catch (FormatException)
        {
            logger.LogWarning("{Name} не является корректным base64 — ключ не используется", name);
        }
        return null;
    }
}
