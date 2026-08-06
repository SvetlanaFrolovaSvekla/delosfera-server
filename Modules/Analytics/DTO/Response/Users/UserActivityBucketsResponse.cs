namespace delosfera_server.Modules.Analytics.DTO.Response.Users;

/// <summary>Распределение пользователей по давности последнего входа — для диаграммы "вовлечённость"</summary>
public class UserActivityBucketsResponse
{
    /// <summary>Заходили сегодня</summary>
    public int Today { get; set; }

    /// <summary>Заходили за последние 7 дней (кроме сегодня)</summary>
    public int Last7Days { get; set; }

    /// <summary>Заходили за последние 30 дней (кроме последних 7)</summary>
    public int Last30Days { get; set; }

    /// <summary>Заходили за последние 90 дней (кроме последних 30)</summary>
    public int Last90Days { get; set; }

    /// <summary>Последний вход более 90 дней назад</summary>
    public int Inactive90PlusDays { get; set; }

    /// <summary>Ни разу не входили в систему</summary>
    public int NeverLoggedIn { get; set; }
}
