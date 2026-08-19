using System.Security.Cryptography;
using System.Text;

namespace delosfera_server.Common.Security;

public interface ISecretProtector
{
    /// <summary>Зашифровать секрет для хранения в базе.</summary>
    string Protect(string plainText);

    /// <summary>Расшифровать секрет, прочитанный из базы.</summary>
    string Unprotect(string cipherText);
}

/// <summary>
/// Шифрование секретов, которые администратор задаёт через интерфейс: пароль
/// сервисной учётной записи службы каталогов и подобные.
///
/// В базе такие пароли нельзя держать открытым текстом: их видит всякий, у кого
/// есть доступ к резервной копии. Хеш здесь не подходит — пароль нужен системе в
/// исходном виде, чтобы предъявить его каталогу.
///
/// Ключ выводится из Jwt:Key, а не хранится отдельно: этот секрет и так задаётся
/// при развёртывании и переживает перезапуски. Смена Jwt:Key делает сохранённые
/// пароли нечитаемыми — их потребуется ввести заново, и это честнее, чем молча
/// расшифровать их подменённым ключом.
/// </summary>
public class SecretProtector : ISecretProtector
{
    private const int NonceSize = 12;   // размер, предписанный AES-GCM
    private const int TagSize = 16;

    private readonly byte[] _key;

    public SecretProtector(IConfiguration configuration)
    {
        var source = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key не задан — без него нельзя защитить пароли интеграций");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(source));
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        // Одной строкой: случайное значение, метка целостности и сам шифртекст.
        var result = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        cipher.CopyTo(result, NonceSize + TagSize);

        return Convert.ToBase64String(result);
    }

    public string Unprotect(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        var raw = Convert.FromBase64String(cipherText);
        if (raw.Length < NonceSize + TagSize)
            throw new InvalidOperationException("Сохранённый секрет повреждён");

        var nonce = raw.AsSpan(0, NonceSize);
        var tag = raw.AsSpan(NonceSize, TagSize);
        var cipher = raw.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
