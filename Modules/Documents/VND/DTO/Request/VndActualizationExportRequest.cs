namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>
/// Запрос на экспорт таблицы "Планирование актуализации" в Excel (кнопка "Экспорт плана в
/// Excel" на странице ActualizationPage). Filter — те же фильтры, что и в обычном поиске
/// (VndSearchRequest), которые пользователь может донастроить прямо в модалке экспорта;
/// Columns — ключи колонок, отмеченных в модалке (как в ACTUALIZATION_COLUMNS на фронте).
/// Обязательные (fixed на фронте) колонки экспортируются всегда, вне зависимости от их
/// наличия здесь — см. VndService.ExportActualizationPlanAsync.
/// </summary>
public class VndActualizationExportRequest
{
    public required VndSearchRequest Filter { get; set; }
    public List<string> Columns { get; set; } = [];

    /// <summary>Чекбокс "Только ни разу не актуализированные" в модалке экспорта — то же самое
    /// пересечение поверх фильтров, что и на странице (см. ActualizationPage.tsx,
    /// displayRows): документы с ровно одной (первой) редакцией. В VndSearchRequest такого
    /// фильтра нет (это не ось поиска, а фильтр в памяти по числу редакций), поэтому
    /// применяется отдельно, уже поверх результата SearchAsync.</summary>
    public bool NeverActualizedOnly { get; set; }
}
