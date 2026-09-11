using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>
/// Настройки ежемесячной сводки по актуализации ВНД — раздел "Уведомления" → "Настройки
/// рассылок" → "Нормотворчество" → "Ежемесячное уведомление — 1-го числа". Запись одна на
/// всю систему, как и ActualizationBucketSettings.
/// </summary>
public class ActualizationNotificationSettings : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Рассылать ли сводку 1-го числа каждого месяца ответственным сотрудникам СП
    /// (ActualizationNotificationResponsible). Выключено по умолчанию — включает администратор
    /// осознанно, после того как настроены ответственные сотрудники.</summary>
    public bool MonthlyDigestEnabled { get; set; }

    /// <summary>
    /// Ключи колонок Excel-вложения сводки, через запятую — те же ключи, что в
    /// ACTUALIZATION_COLUMNS на фронте / VndService.ExportActualizationPlanAsync. Хранится
    /// строкой, а не отдельной таблицей или массивом Postgres: порядок значения не имеет,
    /// набор небольшой, а конвертация в List/из List — в сервисе (см.
    /// ActualizationNotificationService.ParseColumns/FormatColumns).
    /// Пусто — во вложении только обязательные (fixed) колонки.
    /// </summary>
    public string MonthlyDigestColumnsCsv { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
