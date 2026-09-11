namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>
/// Раздел "Уведомления" → "Настройки рассылок" → "Нормотворчество" → "Создать единоразовую
/// рассылку плана актуализации". Получатели — объединение двух источников: целиком составы
/// ответственных сотрудников выбранных СП (ResponsibleOrgUnitIds, см.
/// ActualizationNotificationResponsible) и произвольные отдельные пользователи (UserIds) —
/// как минимум один из двух должен дать хотя бы одного получателя.
/// </summary>
public class SendActualizationOneTimeMailingRequest
{
    /// <summary>СП, чьи ответственные сотрудники за актуализацию включаются в получатели
    /// целиком, группой.</summary>
    public List<int> ResponsibleOrgUnitIds { get; set; } = [];

    /// <summary>Дополнительные получатели — отдельные пользователи, не обязательно входящие в
    /// список ответственных ни одного из выбранных СП.</summary>
    public List<int> UserIds { get; set; } = [];

    public required string Subject { get; set; }
    public required string Message { get; set; }

    /// <summary>Чекбокс "Включить план актуализации" — приложить к письму Excel-план,
    /// собранный теми же настройками (фильтр + колонки), что и кнопка "Экспорт плана в Excel"
    /// (см. ActualizationExportModal на фронте).</summary>
    public bool IncludePlan { get; set; }

    /// <summary>Фильтр и колонки плана для вложения. Обязателен, если IncludePlan = true —
    /// проверяется в ActualizationNotificationService.SendOneTimeMailingAsync.</summary>
    public VndActualizationExportRequest? PlanExport { get; set; }
}
