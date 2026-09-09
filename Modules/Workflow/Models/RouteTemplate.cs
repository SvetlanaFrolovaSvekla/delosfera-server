namespace delosfera_server.Modules.Workflow.Models;

/// <summary>
/// Шаблон типового маршрута (TID-06). Справочник, настраивается без кода.
/// Экземпляр маршрута документа создаётся копированием из шаблона.
/// </summary>
public class RouteTemplate
{
    public int Id { get; set; }

    public delosfera_server.Modules.Documents.Models.DocumentType DocumentType { get; set; }

    public required string Name { get; set; }

    /// <summary>Глобальное правило: авто-включение обязательных согласующих (TID-03).</summary>
    public bool IsGlobalRule { get; set; }

    /// <summary>
    /// Подразделение-инициатор, для которого действует шаблон. null — шаблон уровня типа
    /// (действует для всех подразделений). Селектор выбирает шаблон по (тип + подразделение),
    /// с откатом на шаблон уровня типа, если для подразделения своего нет.
    /// </summary>
    public int? OrgUnitId { get; set; }

    public ICollection<RouteTemplateStep> Steps { get; set; } = new List<RouteTemplateStep>();
}

/// <summary>Этап шаблона маршрута.</summary>
public class RouteTemplateStep
{
    public int Id { get; set; }

    public int RouteTemplateId { get; set; }
    public RouteTemplate? RouteTemplate { get; set; }

    public int Order { get; set; }
    public StepMode Mode { get; set; }
    public StepKind Kind { get; set; }
    public bool IsFinalMethodology { get; set; }
    public int? TimeNormHours { get; set; }

    /// <summary>
    /// Условие включения этапа. null — этап включается всегда. Иначе этап попадает в
    /// маршрут только если строка условия есть в наборе, который контур передаёт при
    /// инстанцировании (у контура есть данные документа для его вычисления). Так один
    /// шаблон описывает и обязательные, и условные этапы (например «Вынесение на
    /// Правление» — только при соответствующем пороге полномочий).
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>Требуемый уровень подписи на этапе (SIG-04); переносится в маршрут при создании.</summary>
    public Signing.Models.SignatureLevel? RequiredSignatureLevel { get; set; }

    public ICollection<RouteTemplateParticipant> Participants { get; set; } = new List<RouteTemplateParticipant>();
}

/// <summary>Участник этапа шаблона.</summary>
public class RouteTemplateParticipant
{
    public int Id { get; set; }

    public int RouteTemplateStepId { get; set; }
    public RouteTemplateStep? RouteTemplateStep { get; set; }

    public int? UserId { get; set; }
    public int? UnitId { get; set; }
    public string? RoleRef { get; set; }
    public bool Required { get; set; }
}
