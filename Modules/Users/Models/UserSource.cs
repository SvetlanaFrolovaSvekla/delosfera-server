namespace delosfera_server.Modules.Users.Models;

/// <summary>
/// Источник учётной записи пользователя
/// </summary>
public enum UserSource
{
    /// <summary>Локальная учётная запись (создана вручную в системе)</summary>
    Local = 1,

    /// <summary>Учётная запись, синхронизированная из LDAP</summary>
    Ldap = 2
}