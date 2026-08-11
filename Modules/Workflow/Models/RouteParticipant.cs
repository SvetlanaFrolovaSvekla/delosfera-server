using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Workflow.Models;

/// <summary>
/// Участник этапа согласования (конкретный пользователь / подразделение / роль).
/// </summary>
public class RouteParticipant
{
    public int Id { get; set; }

    public int RouteStepId { get; set; }
    public RouteStep? RouteStep { get; set; }

    /// <summary>Конкретный согласующий (если задан пользователем).</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Подразделение-согласующий (задача уходит руководителю СП).</summary>
    public int? UnitId { get; set; }

    /// <summary>Ссылка на роль (например "Methodology") для авто-подстановки.</summary>
    public string? RoleRef { get; set; }

    /// <summary>Обязательный согласующий (валидация старта, TID-05).</summary>
    public bool Required { get; set; }

    public ParticipantState State { get; set; } = ParticipantState.Pending;

    public DateTime? ActivatedAt { get; set; }
    public DateTime? DueAt { get; set; }

    public Resolution? Resolution { get; set; }
}
