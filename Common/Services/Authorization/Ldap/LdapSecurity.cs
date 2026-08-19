using System.DirectoryServices.Protocols;

namespace delosfera_server.Common.Services.Authorization.Ldap;

/// <summary>
/// Шифрование связи со службой каталогов.
///
/// Библиотека System.DirectoryServices.Protocols под Linux опирается на OpenLDAP и
/// режим LDAPS (отдельный порт 636 с шифрованием от первого байта) там не
/// поддерживает: попытка заканчивается «The LDAP server is unavailable», хотя тот же
/// сервер прекрасно отвечает на openssl s_client. Зато поддерживается StartTLS —
/// соединение открывается на обычном порту 389 и поднимается до шифрованного до
/// того, как уйдут логин и пароль.
///
/// Для банка важен результат, а не способ: пароль служебной учётной записи не должен
/// ходить по сети открытым текстом. Поэтому шифрование включается StartTLS, а порт
/// 636 остаётся допустимым для случая, когда система работает не под Linux.
///
/// Сертификат домена выписан внутренним удостоверяющим центром. Его корневой
/// сертификат добавлен в доверенные в образе, поэтому проверка идёт обычная —
/// отключать её нельзя: незашифрованное соединение и шифрованное с непроверенным
/// собеседником одинаково не защищают от подмены.
/// </summary>
public static class LdapSecurity
{
    /// <summary>Порт LDAPS, на котором шифрование включается с первого байта.</summary>
    public const int LdapsPort = 636;

    /// <summary>
    /// Включить шифрование до отправки учётных данных. Возвращает способ, которым
    /// это удалось сделать, — для журнала.
    /// </summary>
    public static string Secure(LdapConnection connection, int port)
    {
        connection.SessionOptions.ProtocolVersion = 3;

        if (port == LdapsPort)
        {
            // Отдельный порт LDAPS: под Linux сюда попадать не должны, но если
            // система работает под Windows, этот путь рабочий.
            connection.SessionOptions.SecureSocketLayer = true;
            connection.SessionOptions.VerifyServerCertificate =
                (_, cert) => LdapCertificateValidator.VerifyCorporateCertificate(cert);
            return "LDAPS";
        }

        // StartTLS на обычном порту: соединение поднимается до шифрованного прежде,
        // чем по нему уйдут логин и пароль.
        connection.SessionOptions.StartTransportLayerSecurity(null);
        return "StartTLS";
    }
}
