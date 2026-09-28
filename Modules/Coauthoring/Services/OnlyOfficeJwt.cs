using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace delosfera_server.Modules.Coauthoring.Services;

/// <summary>
/// Подпись и проверка JWT в формате, который понимает OnlyOffice Document Server
/// (COOP-1). Payload'ом служит произвольный JSON-объект (весь конфиг редактора либо
/// тело callback'а), а не плоский набор claim'ов, поэтому токен собирается вручную
/// по RFC 7519 (header.payload.signature, HS256) — так payload попадает в токен
/// как есть, без разворачивания в claim'ы.
/// </summary>
public static class OnlyOfficeJwt
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // OnlyOffice сверяет подписанный payload с телом запроса по значениям полей;
        // null-поля из конфига в токен не кладём, чтобы сравнение было предсказуемым.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Подписать объект-payload общим секретом. Возвращает компактный JWT.</summary>
    public static string Sign(object payload, string secret)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var body = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var signingInput = $"{header}.{body}";
        var signature = Base64UrlEncode(ComputeSignature(signingInput, secret));
        return $"{signingInput}.{signature}";
    }

    /// <summary>
    /// Проверить подпись и вернуть payload как <see cref="JsonElement"/>. Возвращает
    /// false при неверной подписи, битом формате или истёкшем exp — вызывающий код
    /// не должен доверять телу callback'а без прохождения этой проверки.
    /// </summary>
    public static bool TryValidate(string token, string secret, out JsonElement payload)
    {
        payload = default;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.');
        if (parts.Length != 3) return false;

        var expected = Base64UrlEncode(ComputeSignature($"{parts[0]}.{parts[1]}", secret));

        // Сравнение в постоянном времени: обычное сравнение строк утекает длину
        // совпавшего префикса и открывает подбор подписи по времени ответа.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2])))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            if (doc.RootElement.TryGetProperty("exp", out var exp)
                && exp.ValueKind == JsonValueKind.Number
                && exp.TryGetInt64(out var expUnix)
                && DateTimeOffset.FromUnixTimeSeconds(expUnix) < DateTimeOffset.UtcNow)
                return false;

            payload = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static byte[] ComputeSignature(string signingInput, string secret) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signingInput));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
