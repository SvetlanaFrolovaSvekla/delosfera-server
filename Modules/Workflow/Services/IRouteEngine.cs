using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

/// <summary>
/// Конечный автомат маршрута согласования (TID-01..14). Все переходы — явные методы.
/// </summary>
public interface IRouteEngine
{
    /// <summary>Создать экземпляр маршрута из шаблона (копирование этапов/участников).</summary>
    Task<RouteInstance> InstantiateFromTemplateAsync(
        int documentId, int templateId, IReadOnlySet<string>? satisfiedConditions = null);

    /// <summary>Запустить маршрут: валидация (обяз. согласующие, ОМ финальный) + активация первого этапа.</summary>
    Task StartAsync(int routeInstanceId, int actorUserId);

    /// <summary>Резолюция участника (согласовано / с замечаниями / отклонить / вето).</summary>
    Task ResolveAsync(int participantId, ResolutionType type, string? comment, int actorUserId, int? signatureId = null);

    /// <summary>
    /// Маршрут из явного списка согласующих, без шаблона.
    ///
    /// Шаблон описывает типовой путь документа, но у служебных записок состав
    /// согласующих чаще определяет автор под конкретный вопрос.
    /// </summary>
    /// <summary>Дописать этап подписания в конец маршрута, собранного по шаблону.</summary>
    Task AppendSigningStepAsync(int routeInstanceId, int signerUserId);

    /// <summary>Маршрут из одного подписанта — когда согласование уже пройдено.</summary>
    Task<RouteInstance> InstantiateForSignerAsync(int documentId, int signerUserId, int? timeNormHours = null);

    Task<RouteInstance> InstantiateForApproversAsync(
        int documentId, IReadOnlyList<int> approverUserIds, bool parallel,
        int? timeNormHours = null, int? signerUserId = null);

    /// <summary>Подтверждение автором «Замечания устранены» (строгий режим, TID-09).</summary>
    Task ConfirmRemarkResolvedAsync(int remarkId, int actorUserId);

    /// <summary>Применить автоакцепт к просроченным участникам (вызывается воркером таймеров, TID-08).</summary>
    Task ApplyOverdueAsync(DateTime now);

    /// <summary>
    /// Прервать маршрут по инициативе контура (отзыв документа): аннулировать незакрытых
    /// участников и их задачи, экземпляр пометить прерванным. История резолюций сохраняется.
    /// </summary>
    Task InterruptAsync(int routeInstanceId, int actorUserId);

    /// <summary>
    /// Новый круг того же маршрута: текущий экземпляр прерывается (резолюции остаются в истории),
    /// создаётся копия его этапов и действующих участников и запускается с первого этапа.
    /// </summary>
    Task<RouteInstance> RestartAsync(int routeInstanceId, int actorUserId);

    /// <summary>
    /// Добавить согласующего в идущий маршрут — отдельным последовательным этапом. Место — перед
    /// этапом <paramref name="beforeStepOrder"/> (только после текущего), по умолчанию — перед
    /// этапами подписания / финального контроля, а если их нет — в конец.
    /// </summary>
    Task<RouteParticipant> AddApproverStepAsync(int routeInstanceId, int userId, int? beforeStepOrder, int actorUserId);

    /// <summary>
    /// Убрать из идущего маршрута участника, который ещё не вынес решения. Если он был активен,
    /// его задача снимается и маршрут идёт дальше, как после его решения.
    /// </summary>
    Task RemoveParticipantAsync(int participantId, int actorUserId);

    /// <summary>
    /// Заменить участника идущего маршрута, ещё не вынесшего решения, другим сотрудником на том
    /// же этапе (смена согласующего или подписанта). Маршрут при этом не перезапускается и не
    /// продвигается: если задача уже была выдана, она снимается у прежнего и выдаётся новому.
    /// </summary>
    Task<RouteParticipant> ReplaceParticipantAsync(int participantId, int newUserId, int actorUserId);

    /// <summary>
    /// Пересобрать ещё не пройденную часть согласования идущего маршрута по новому списку
    /// согласующих и режиму (параллельно / по очереди) — без перезапуска круга. Вынесенные
    /// решения остаются; кто уже высказался, заново не ставится. Этапы подписания и финального
    /// контроля остаются в хвосте. Задача, уже выданная согласующему, сохраняется, если он и
    /// дальше должен решать сейчас, иначе снимается.
    /// </summary>
    Task RebuildApprovalAsync(int routeInstanceId, IReadOnlyList<int> approverUserIds, bool parallel, int actorUserId);
}
