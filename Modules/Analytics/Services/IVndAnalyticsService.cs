using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.DTO.Response;
using delosfera_server.Modules.Analytics.DTO.Response.Vnd;

namespace delosfera_server.Modules.Analytics.Services;

/// <summary>Аналитика по модулю ВНД: сводки и данные для графиков страницы отчётности</summary>
public interface IVndAnalyticsService
{
    /// <summary>KPI-плашки: количество по статусам, просрочки, активные согласования, средние сроки</summary>
    Task<VndOverviewResponse> GetOverviewAsync();

    /// <summary>Распределение документов по статусам (для круговой диаграммы)</summary>
    Task<List<ChartCategoryPoint>> GetStatusDistributionAsync(string language);

    /// <summary>Распределение документов по видам (типам) ВНД</summary>
    Task<List<ChartCategoryPoint>> GetTypeDistributionAsync(string language);

    /// <summary>Распределение документов по подразделениям-разработчикам (топ N + "Остальные")</summary>
    Task<List<ChartCategoryPoint>> GetDeveloperDistributionAsync(string language, int top = 10);

    /// <summary>Распределение документов по уровням секретности</summary>
    Task<List<ChartCategoryPoint>> GetSecurityLevelDistributionAsync(string language);

    /// <summary>Распределение документов по рубрикам классификатора (топ N)</summary>
    Task<List<ChartCategoryPoint>> GetRubricDistributionAsync(string language, int top = 10);

    /// <summary>Облако ключевых слов: топ N самых используемых ключевых слов и количество документов</summary>
    Task<List<ChartCategoryPoint>> GetKeywordCloudAsync(string language, int top = 30);

    /// <summary>Динамика жизненного цикла ВНД по периодам: создано / отправлено на согласование /
    /// опубликовано / архивировано</summary>
    Task<List<VndDynamicsPoint>> GetDynamicsAsync(AnalyticsPeriodRequest request);

    /// <summary>Динамика циклов актуализации по периодам: сколько запущено, сколько завершено,
    /// доля с реальными изменениями, средняя длительность</summary>
    Task<List<VndActualizationTrendPoint>> GetActualizationTrendAsync(AnalyticsPeriodRequest request);

    /// <summary>Эффективность процесса согласования: доля успешных, доля с доработками,
    /// средняя/медианная длительность и тренд по периодам</summary>
    Task<VndApprovalPerformanceResponse> GetApprovalPerformanceAsync(AnalyticsPeriodRequest? request);

    /// <summary>Загрузка согласующих подразделений (или конкретных согласующих пользователей) —
    /// поиск "узких мест" маршрута согласования по доле решений, зачтённых по таймауту</summary>
    Task<List<VndApproverWorkloadItem>> GetApproverWorkloadAsync(bool byUser = false);

    /// <summary>Матрица "подразделение-разработчик × статус" — данные для тепловой карты</summary>
    Task<List<VndOrgUnitStatusMatrixItem>> GetOrgUnitStatusMatrixAsync(string language);
}
