namespace delosfera_server.Modules.Workflow.Models;

/// <summary>
/// Этап маршрута (последовательный/параллельный/финальный/подписание/Правление).
/// </summary>
public class RouteStep
{
    public int Id { get; set; }

    public int RouteInstanceId { get; set; }
    public RouteInstance? RouteInstance { get; set; }

    public int Order { get; set; }

    public StepMode Mode { get; set; }
    public StepKind Kind { get; set; }

    /// <summary>Финальный контроль Отдела методологии (TID-04): всегда последний.</summary>
    public bool IsFinalMethodology { get; set; }

    /// <summary>Норматив времени на этап в часах (TID-12); null — без автоакцепта.</summary>
    public int? TimeNormHours { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public ICollection<RouteParticipant> Participants { get; set; } = new List<RouteParticipant>();
}
