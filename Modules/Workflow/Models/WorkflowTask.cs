namespace delosfera_server.Modules.Workflow.Models;

/// <summary>
/// Задача участнику маршрута («Мои задачи», GEN-11). Создаётся при активации участника,
/// закрывается при резолюции, эскалируется при просрочке норматива.
/// </summary>
public class WorkflowTask
{
    public int Id { get; set; }

    public int? RouteParticipantId { get; set; }

    /// <summary>
    /// Документ задачи, когда она не из маршрута: решение адресата и поручение
    /// живут вне согласования, но в общем списке задач должны быть наравне с ним —
    /// иначе работа человека разложена по двум разным экранам.
    /// </summary>
    public int? DocumentId { get; set; }

    /// <summary>
    /// Запись контура, породившая задачу (например поручение по записке): по ней
    /// задача закрывается, когда работа сдана в своём контуре.
    /// </summary>
    public int? SourceEntityId { get; set; }

    public int AssigneeUserId { get; set; }

    /// <summary>Тип задачи, напр. "Approval", "RemarksResolution", "AddresseeDecision", "Assignment".</summary>
    public required string Type { get; set; }

    public DateTime? DueAt { get; set; }

    public WorkflowTaskState State { get; set; } = WorkflowTaskState.Open;

    public int? EscalatedToUserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
