namespace delosfera_server.Modules.Notifications.DTO.Response;

/// <summary>Минимум о уведомлении для «выбрать всё по фильтру»: id и флаги, по которым
/// кнопки массовых действий решают, что применимо к выбранному.</summary>
public class NotificationSelectionItem
{
    public int Id { get; set; }
    public bool IsRead { get; set; }
    public bool IsFavorite { get; set; }
}
