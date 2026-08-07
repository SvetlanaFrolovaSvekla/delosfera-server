using delosfera_server.Modules.Documents.VND.DTO.Response;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface ITasksService
{
    Task<List<VndTaskResponse>> GetCoordinationTasksAsync(int userId);
    Task<List<VndTaskResponse>> GetActualizationTasksAsync(int userId);
    Task<List<VndTaskResponse>> GetConsolidationTasksAsync(int userId);
    Task<List<VndTaskResponse>> GetMyVndApprovalTasksAsync(int userId);
    Task<VndTaskCountsResponse> GetCountsAsync(int userId);

    /// <summary>Сводка персональных KPI для карточек на главной странице</summary>
    Task<VndHomeSummaryResponse> GetHomeSummaryAsync(int userId);
}