namespace delosfera_server.Modules.Notifications.Models;

// TODO: Завести категорию для актуализации

public enum NotificationCategory
{
    System = 0,       // Системные - обновления, тех. работы
    Vnd = 1,          // ВНД - новые редакции, статусы
    Approval = 2,     // согласования - новые этапы, решения, дедлайны
    Task = 3,         // задачи/поручения
    Other = 4         // разное
}