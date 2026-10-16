namespace delosfera_server.Modules.Notifications.DTO.Request;

/// <summary>Массовое действие над своими уведомлениями. Чужие и удалённые id игнорируются.</summary>
public class NotificationBulkRequest
{
    public List<int> Ids { get; set; } = [];

    /// <summary>Только для действия «избранное»: true — добавить, false — убрать.</summary>
    public bool Favorite { get; set; }
}
