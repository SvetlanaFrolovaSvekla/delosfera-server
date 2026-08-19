using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

/// <summary>
/// Реакция контура на завершение маршрута согласования. Движок не знает про ВНД, СЗ и закупки —
/// он лишь сообщает, что маршрут дошёл до конца, а обработчик своего контура решает, что делать.
/// </summary>
public interface IRouteCompletionHandler
{
    /// <summary>
    /// Маршрут утверждён (все этапы пройдены). documentId — идентификатор документа контура,
    /// с которым маршрут был создан. Обработчик сам проверяет, его ли это документ.
    /// </summary>
    Task OnRouteApprovedAsync(int routeInstanceId, int documentId, int actorUserId);

    /// <summary>
    /// Маршрут сменил статус: отклонён, ушёл на доработку или возобновлён после неё.
    /// Контуры, которым нужно вести собственный статус документа синхронно с маршрутом,
    /// переопределяют этот метод; остальным достаточно события утверждения.
    /// </summary>
    Task OnRouteStatusChangedAsync(
        int routeInstanceId, int documentId, RouteInstanceStatus status, int actorUserId)
        => Task.CompletedTask;
}
