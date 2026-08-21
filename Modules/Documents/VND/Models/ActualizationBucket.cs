namespace delosfera_server.Modules.Documents.VND.Models;

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
/// Пороги в днях для расчёта ActualizationBucket. Значения настраиваются администратором
/// в справочнике "Пороги индикации сроков актуализации" (раздел ВНД, см.
/// ActualizationBucketSettings/ActualizationBucketSettingsService) и хранятся в БД —
/// здесь только текущий кэш в памяти процесса, обновляемый этим сервисом при каждом
/// чтении/сохранении настроек. "Просрочено" порогом не управляется в принципе: это
/// всегда дата актуализации в прошлом.
/// </summary>
public static class ActualizationThresholds
{
    public static int CriticalDays { get; private set; } = 5;
    public static int ApproachingDays { get; private set; } = 30;

    /// <summary>Обновляет пороги в памяти значениями из БД. Вызывается
    /// ActualizationBucketSettingsService — не вызывайте напрямую.</summary>
    public static void Configure(int criticalDays, int approachingDays)
    {
        CriticalDays = criticalDays;
        ApproachingDays = approachingDays;
    }

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