using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.DTO.Response;
using delosfera_server.Modules.Analytics.DTO.Response.Users;

namespace delosfera_server.Modules.Analytics.Services;

/// <summary>Аналитика по модулю "Пользователи": сводки, вовлечённость и связка с активностью по ВНД</summary>
public interface IUserAnalyticsService
{
    /// <summary>KPI-плашки: всего/активных/новых пользователей, вовлечённость, количество ролей</summary>
    Task<UserOverviewResponse> GetOverviewAsync();

    /// <summary>Динамика регистраций новых пользователей по периодам</summary>
    Task<List<ChartTimePoint>> GetRegistrationTrendAsync(AnalyticsPeriodRequest request);

    /// <summary>Распределение пользователей по ролям</summary>
    Task<List<ChartCategoryPoint>> GetRoleDistributionAsync(string language);

    /// <summary>Распределение пользователей по подразделениям (топ N + "Остальные")</summary>
    Task<List<ChartCategoryPoint>> GetOrgUnitDistributionAsync(string language, int top = 10);

    /// <summary>Распределение по давности последнего входа: сегодня / 7 дней / 30 дней / 90 дней /
    /// давно / никогда не входили - индикатор вовлечённости</summary>
    Task<UserActivityBucketsResponse> GetActivityBucketsAsync();

    /// <summary>Топ пользователей-инициаторов по количеству созданных/актуализируемых ВНД</summary>
    Task<List<UserTopInitiatorItem>> GetTopInitiatorsAsync(int top = 10);

    /// <summary>Персональная статистика пользователей как согласующих по ВНД (скорость принятия решений)</summary>
    Task<List<UserApproverPerformanceItem>> GetApproverPerformanceAsync(int top = 20);
}
