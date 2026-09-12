using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface ITasksService
{
    /// <summary>"Ждущие моего согласования" — включает первичное, повторное согласование
    /// и финальную выдержку одним списком.</summary>
    Task<List<VndTaskResponse>> GetCoordinationTasksAsync(int userId);
    Task<List<VndTaskResponse>> GetActualizationTasksAsync(int userId);
    Task<List<VndTaskResponse>> GetConsolidationTasksAsync(int userId);
    Task<List<VndTaskResponse>> GetMyVndApprovalTasksAsync(int userId);

    /// <summary>"Отклонено" — для инициатора: последний процесс согласования по документу
    /// завершился отклонением, и новый цикл согласования по нему ещё не запускался. Раньше
    /// после отклонения инициатор не получал отдельной задачи — документ просто возвращался в
    /// "Черновик"/"На актуализации" и терялся среди обычных задач, требуя внимания только по
    /// уведомлению.</summary>
    Task<List<VndTaskResponse>> GetRejectedTasksAsync(int userId);
    Task<VndTaskCountsResponse> GetCountsAsync(int userId);

    /// <summary>Сводка персональных KPI для карточек на главной странице</summary>
    Task<VndHomeSummaryResponse> GetHomeSummaryAsync(int userId);

    /// <summary>Подробная сводка по просрочкам согласования текущего пользователя (месяц/год/
    /// всего + список конкретных ВНД) — для блока "Мои показатели" в Аналитике
    /// (ВНД → Актуализация).</summary>
    Task<VndMyTimeoutApprovalsResponse> GetMyTimeoutApprovalsAsync(int userId);

    // --- История "Выполнено" по каждому разделу — с пагинацией, т.к. список может расти
    // без ограничения по времени (см. TasksService для точного критерия "выполнено" в каждом
    // случае: решение принято / актуализация выполнена / документ опубликован / отправлено
    // повторно после отклонения).
    Task<PagedResult<VndTaskResponse>> GetCoordinationDoneTasksAsync(int userId, int page, int pageSize);
    Task<PagedResult<VndTaskResponse>> GetMyVndApprovalDoneTasksAsync(int userId, int page, int pageSize);
    Task<PagedResult<VndTaskResponse>> GetActualizationDoneTasksAsync(int userId, int page, int pageSize);
    Task<PagedResult<VndTaskResponse>> GetConsolidationDoneTasksAsync(int userId, int page, int pageSize);
    Task<PagedResult<VndTaskResponse>> GetRejectedDoneTasksAsync(int userId, int page, int pageSize);
}