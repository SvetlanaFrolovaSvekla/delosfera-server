using System.Security.Cryptography.X509Certificates;

namespace delosfera_server.Common.Services.Authorization.Ldap;

// Проверка сертификата при подключении сервера к AD по LDAPS (зашифрованное соединение),
// AD-сервер присылает свой SSL-сертификат. Код должен решить: доверять этому сертификату или нет.

/*
    X509Chain - это встроенный в .NET механизм, который проверяет сертификат так же,
    как это делает обычный браузер: строит "цепочку доверия" от сертификата сервера
    вверх, до какого-то корневого сертификата (CA — Certificate Authority), к которому
    есть доверие.

    chain.Build(cert2) возвращает true, если вся цепочка выстроилась без проблем,
    и false, если что-то не сошлось.

    ТУТ ОТКЛЮЧЕНА ПРОВЕРКА НА ОТОЗВАННОСТЬ на всякий случай, проверяется только цепочка
*/
public static class LdapCertificateValidator
{
    /// <summary>
    /// Строит цепочку доверия для сертификата и проверяет её валидность.
    /// </summary>
    public static bool VerifyCorporateCertificate(X509Certificate certificate)
    {
        using var cert2 = new X509Certificate2(certificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(cert2);
    }
}