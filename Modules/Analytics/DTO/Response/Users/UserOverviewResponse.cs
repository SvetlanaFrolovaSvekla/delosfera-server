namespace delosfera_server.Modules.Analytics.DTO.Response.Users;

/// <summary>Сводные показатели-карточки по пользователям для страницы отчётности</summary>
public class UserOverviewResponse
{
    /// <summary>Всего пользователей в системе</summary>
    public int Total { get; set; }

    /// <summary>Активные (IsActive == true)</summary>
    public int Active { get; set; }

    /// <summary>Заблокированные/деактивированные</summary>
    public int Inactive { get; set; }

    /// <summary>Заходили в систему за последние 7 дней</summary>
    public int ActiveLast7Days { get; set; }

    /// <summary>Заходили в систему за последние 30 дней</summary>
    public int ActiveLast30Days { get; set; }

    /// <summary>Ни разу не заходили в систему (LastLoginAt == null)</summary>
    public int NeverLoggedIn { get; set; }

    /// <summary>Новых пользователей за последние 30 дней</summary>
    public int CreatedLast30Days { get; set; }

    /// <summary>Количество ролей в системе</summary>
    public int RolesCount { get; set; }

    /// <summary>Количество подразделений, в которых есть хотя бы один пользователь</summary>
    public int OrgUnitsWithUsersCount { get; set; }
}
