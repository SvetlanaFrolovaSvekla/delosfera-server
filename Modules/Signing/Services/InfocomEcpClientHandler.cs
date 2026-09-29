using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>HTTP-обработчик для клиента к delosfera-ecp-service. Если задан
/// InfocomEcpService:ServerCertificateSha256, принимается только сертификат с этим SHA-256 отпечатком
/// (удобно для самоподписанного сертификата ecp-service без корпоративного УЦ). Иначе — обычная проверка
/// сертификата .NET (цепочка до доверенного УЦ + имя хоста).</summary>
public static class InfocomEcpClientHandler
{
    public static HttpMessageHandler Create(InfocomEcpServiceOptions options, ILogger logger)
    {
        var handler = new SocketsHttpHandler();
        var pin = NormalizeThumbprint(options.ServerCertificateSha256);
        if (pin is null) return handler;

        handler.SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null) return false;
                var actual = Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));
                var ok = CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(actual), System.Text.Encoding.ASCII.GetBytes(pin));
                if (!ok)
                    logger.LogError("Сертификат delosfera-ecp-service не совпал с InfocomEcpService:ServerCertificateSha256 " +
                                    "(получен {Actual}) — соединение отклонено", actual);
                return ok;
            },
        };
        return handler;
    }

    private static string? NormalizeThumbprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var hex = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return hex.Length == 64 ? hex : throw new InvalidOperationException(
            "InfocomEcpService:ServerCertificateSha256 должен быть SHA-256 отпечатком (64 hex-символа)");
    }
}
