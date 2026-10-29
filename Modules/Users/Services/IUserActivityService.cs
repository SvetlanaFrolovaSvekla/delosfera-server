using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;

namespace delosfera_server.Modules.Users.Services;

public interface IUserActivityService
{
    /// <summary>Лента активности пользователя, собранная из данных ВНД (созданные документы,
    /// принятые решения по согласованию, инициированные согласования). Постраничная (курсор),
    /// с отбором по периоду, тексту и видам действий — всё фильтруется в базе.</summary>
    Task<UserActivityResponse> GetActivityAsync(int userId, string languageCode, UserActivityQuery query);
}
