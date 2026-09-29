using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>Настройки сохранённой ЭЦП-идентификации (см. appsettings.json → EcpCredentials).</summary>
public class EcpCredentialOptions
{
    public const string Section = "EcpCredentials";

    /// <summary>Ключ шифрования кода — base64 от РОВНО 32 байт (AES-256). НЕ хранить в appsettings.json:
    /// задаётся переменной окружения EcpCredentials__EncryptionKey (прод/тест) или user-secrets (dev).
    /// Пустой/некорректный ключ = функция выключена (сохранить идентификацию нельзя, подписание идёт
    /// по обычному флоу с вводом ПИН и кода). Сгенерировать: openssl rand -base64 32.
    /// ВАЖНО: при смене ключа все сохранённые идентификации перестают расшифровываться — пользователям
    /// придётся пройти идентификацию заново.</summary>
    public string EncryptionKey { get; set; } = "";

    /// <summary>Сколько дней действует сохранённая идентификация с момента её создания.</summary>
    public int RetentionDays { get; set; } = 7;
}

public interface IEcpCredentialProtector
{
    /// <summary>true — ключ шифрования задан и корректен.</summary>
    bool IsConfigured { get; }

    string Protect(int userId, string personIdnp, string code);

    /// <summary>null — не удалось расшифровать (другой ключ, повреждённая строка, чужая учётка/ПИН).</summary>
    string? Unprotect(int userId, string personIdnp, string protectedValue);
}

/// <summary>AES-256-GCM. Формат: base64(nonce[12] || tag[16] || ciphertext). В AAD входят версия схемы,
/// id пользователя и ПИН — расшифровка с другими значениями не удастся. Собственная реализация, а не
/// ASP.NET Data Protection, потому что ключи Data Protection по умолчанию лежат в файловой системе
/// контейнера и теряются при его пересоздании, а здесь ключ явно задаётся конфигурацией.</summary>
public sealed class EcpCredentialProtector : IEcpCredentialProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[]? _key;

    public EcpCredentialProtector(IOptions<EcpCredentialOptions> options, ILogger<EcpCredentialProtector> logger)
    {
        var raw = options.Value.EncryptionKey?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            logger.LogInformation("EcpCredentials:EncryptionKey не задан — сохранение ЭЦП-идентификации выключено");
            return;
        }

        try
        {
            var key = Convert.FromBase64String(raw);
            if (key.Length == 32) _key = key;
            else logger.LogWarning("EcpCredentials:EncryptionKey должен быть base64 от 32 байт (сейчас {Length}) — функция выключена", key.Length);
        }
        catch (FormatException)
        {
            logger.LogWarning("EcpCredentials:EncryptionKey не является корректным base64 — функция выключена");
        }
    }

    public bool IsConfigured => _key is not null;

    public string Protect(int userId, string personIdnp, string code)
    {
        if (_key is null) throw new InvalidOperationException("Ключ шифрования ЭЦП-идентификации не задан");

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(code);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, Aad(userId, personIdnp));

        var result = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        cipher.CopyTo(result, NonceSize + TagSize);
        return Convert.ToBase64String(result);
    }

    public string? Unprotect(int userId, string personIdnp, string protectedValue)
    {
        if (_key is null) return null;
        try
        {
            var data = Convert.FromBase64String(protectedValue);
            if (data.Length < NonceSize + TagSize) return null;

            var nonce = data.AsSpan(0, NonceSize);
            var tag = data.AsSpan(NonceSize, TagSize);
            var cipher = data.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, Aad(userId, personIdnp));
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    private static byte[] Aad(int userId, string personIdnp) =>
        Encoding.UTF8.GetBytes($"ecp-credential:v1:{userId}:{personIdnp}");
}
