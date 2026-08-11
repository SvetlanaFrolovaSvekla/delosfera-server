using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

/// <summary>
/// Конечный автомат маршрута согласования (TID-01..14). Все переходы — явные методы.
/// </summary>
public interface IRouteEngine
{
    /// <summary>Создать экземпляр маршрута из шаблона (копирование этапов/участников).</summary>
    Task<RouteInstance> InstantiateFromTemplateAsync(int documentId, int templateId);

    /// <summary>Запустить маршрут: валидация (обяз. согласующие, ОМ финальный) + активация первого этапа.</summary>
    Task StartAsync(int routeInstanceId, int actorUserId);

    /// <summary>Резолюция участника (согласовано / с замечаниями / отклонить / вето).</summary>
    Task ResolveAsync(int participantId, ResolutionType type, string? comment, int actorUserId, int? signatureId = null);

    /// <summary>Подтверждение автором «Замечания устранены» (строгий режим, TID-09).</summary>
    Task ConfirmRemarkResolvedAsync(int remarkId, int actorUserId);

    /// <summary>Применить автоакцепт к просроченным участникам (вызывается воркером таймеров, TID-08).</summary>
    Task ApplyOverdueAsync(DateTime now);

    /// <summary>
    /// Прервать маршрут по инициативе контура (отзыв документа): аннулировать незакрытых
    /// участников и их задачи, экземпляр пометить прерванным. История резолюций сохраняется.
    /// </summary>
    Task InterruptAsync(int routeInstanceId, int actorUserId);
}
