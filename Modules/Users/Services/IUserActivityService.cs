using delosfera_server.Modules.Users.DTO.Response;

namespace delosfera_server.Modules.Users.Services;

public interface IUserActivityService
{
    /// <summary>Сводка активности пользователя, собранная из данных ВНД (созданные документы,
    /// принятые решения по согласованию, инициированные согласования).</summary>
    Task<UserActivityResponse> GetActivityAsync(int userId, string languageCode, int recentLimit = 20);
}
