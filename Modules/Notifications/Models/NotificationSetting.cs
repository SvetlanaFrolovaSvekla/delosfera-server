namespace delosfera_server.Modules.Notifications.Models;

/// <summary>
/// Персональные настройки уведомлений (УВ-16). Пока одна настройка — получать ли
/// утренний email-дайджест; заведена отдельной сущностью, чтобы дальше сюда легли и
/// другие переключатели (каналы, категории) без ломки схемы.
/// </summary>
public class NotificationSetting
{
    public int Id { get; set; }

    /// <summary>Владелец настроек (одна запись на пользователя).</summary>
    public int UserId { get; set; }

    /// <summary>Получать утренний email-дайджест (УВ-15). По умолчанию да.</summary>
    public bool EmailDigestEnabled { get; set; } = true;

    public DateTime UpdatedAt { get; set; }
}
