using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IActualizationNotificationService
{
    /// <summary>Все ответственные сотрудники за актуализацию, по всем СП сразу — клиент
    /// группирует по OrgUnitId.</summary>
    Task<List<ActualizationNotificationResponsibleResponse>> GetResponsiblesAsync();

    /// <summary>Полная замена состава ответственных для одного СП (см.
    /// SetActualizationNotificationResponsiblesRequest). Ответственный не обязан относиться к
    /// этому же СП — назначить можно и сотрудника другого подразделения (см. комментарий у
    /// SetResponsiblesAsync). Возвращает обновлённый полный список (по всем СП), как и
    /// GetResponsiblesAsync.</summary>
    Task<List<ActualizationNotificationResponsibleResponse>> SetResponsiblesAsync(
        SetActualizationNotificationResponsiblesRequest request);

    Task<ActualizationNotificationSettingsResponse> GetSettingsAsync();
    Task<ActualizationNotificationSettingsResponse> UpdateSettingsAsync(
        UpdateActualizationNotificationSettingsRequest request);

    /// <summary>Демонстрация письма ежемесячной сводки для одного СП — без отправки.</summary>
    Task<ActualizationNotificationPreviewResponse> PreviewMonthlyDigestAsync(int orgUnitId, string languageCode);

    /// <summary>
    /// Разослать ежемесячную сводку 1-го числа (см. ActualizationNotificationWorker). Ничего не
    /// делает и возвращает 0, если сегодня не 1-е число или рассылка выключена в настройках —
    /// эти проверки внутри, а не только в вызывающем воркере, чтобы метод был безопасен и при
    /// ручном вызове (например, из будущего "отправить сейчас" в интерфейсе).
    /// </summary>
    Task<int> SendMonthlyDigestAsync(DateOnly today, CancellationToken ct = default);
}
