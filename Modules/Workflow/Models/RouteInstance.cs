namespace delosfera_server.Modules.Workflow.Models;

/// <summary>
/// Экземпляр маршрута согласования конкретного документа. Управляется RouteEngine.
/// </summary>
public class RouteInstance
{
    public int Id { get; set; }

    /// <summary>Документ (Documents.Document), к которому относится маршрут.</summary>
    public int DocumentId { get; set; }

    /// <summary>Шаблон-источник (RouteTemplate), если создан из шаблона.</summary>
    public int? TemplateId { get; set; }

    public RouteInstanceStatus Status { get; set; } = RouteInstanceStatus.Draft;

    /// <summary>Порядок текущего активного этапа.</summary>
    public int CurrentStepOrder { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public ICollection<RouteStep> Steps { get; set; } = new List<RouteStep>();
}
