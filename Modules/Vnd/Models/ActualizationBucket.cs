namespace delosfera_server.Modules.Vnd.Models;

/// <summary>
/// Статус срока актуализации ВНД относительно текущей даты.
/// Вычисляется на лету от DueActualizationDate, в БД не хранится
/// </summary>
public enum ActualizationBucket
{
    Normal = 0,       // > 30 дней до срока
    Approaching = 1,  // от 6 до 30 дней
    Critical = 2,     // от 0 до 5 дней
    Overdue = 3        // срок уже прошёл
}

/// <summary>
/// Пороги в днях для расчёта ActualizationBucket
/// </summary>
public static class ActualizationThresholds
{
    public const int CriticalDays = 5;
    public const int ApproachingDays = 30;

    public static ActualizationBucket? Resolve(DateOnly? dueDate, DateOnly today)
    {
        if (!dueDate.HasValue) return null;

        if (dueDate.Value < today) return ActualizationBucket.Overdue;

        var daysLeft = dueDate.Value.DayNumber - today.DayNumber;

        if (daysLeft <= CriticalDays) return ActualizationBucket.Critical;
        if (daysLeft <= ApproachingDays) return ActualizationBucket.Approaching;

        return ActualizationBucket.Normal;
    }
}