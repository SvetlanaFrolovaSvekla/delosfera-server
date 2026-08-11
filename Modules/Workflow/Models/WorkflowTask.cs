namespace delosfera_server.Modules.Workflow.Models;

/// <summary>
/// Задача участнику маршрута («Мои задачи», GEN-11). Создаётся при активации участника,
/// закрывается при резолюции, эскалируется при просрочке норматива.
/// </summary>
public class WorkflowTask
{
    public int Id { get; set; }

    public int? RouteParticipantId { get; set; }

    public int AssigneeUserId { get; set; }

    /// <summary>Тип задачи, напр. "Approval", "RemarksResolution".</summary>
    public required string Type { get; set; }

    public DateTime? DueAt { get; set; }

    public WorkflowTaskState State { get; set; } = WorkflowTaskState.Open;

    public int? EscalatedToUserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
