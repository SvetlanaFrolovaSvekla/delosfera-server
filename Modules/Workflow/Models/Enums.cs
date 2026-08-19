namespace delosfera_server.Modules.Workflow.Models;

/// <summary>Статус экземпляра маршрута согласования.</summary>
public enum RouteInstanceStatus
{
    Draft = 0,
    Running = 1,
    OnRevision = 2,   // на доработке у инициатора (строгий режим замечаний)
    Approved = 3,
    Rejected = 4,
    Interrupted = 5,  // прерван (санкции за просрочку инициатора, TID-13)
    Arbitration = 6   // вынесен на арбитраж Правления
}

/// <summary>Режим этапа: последовательный или параллельный (TID-02).</summary>
public enum StepMode
{
    Sequential = 0,
    Parallel = 1
}

/// <summary>Назначение этапа.</summary>
public enum StepKind
{
    Approval = 0,
    FinalControl = 1, // финальный контроль Отдела методологии (TID-04)
    Signing = 2,
    Board = 3         // вынесение на Правление/арбитраж
}

/// <summary>Резолюция участника (TID-07).</summary>
public enum ResolutionType
{
    Approved = 0,
    ApprovedWithRemarks = 1,
    Rejected = 2,
    AutoAccept = 3,   // системная (TID-08)
    Veto = 4
}

/// <summary>Состояние участника на этапе.</summary>
public enum ParticipantState
{
    Pending = 0,   // ещё не дошла очередь
    Active = 1,    // ожидает решения
    Done = 2,      // вынес резолюцию
    Cancelled = 3  // аннулирован (отклонение в параллельном этапе, TID-11)
}

/// <summary>Состояние замечания (строгий режим, TID-09).</summary>
public enum RemarkState
{
    Open = 0,
    Resolved = 1
}

/// <summary>Состояние задачи.</summary>
public enum WorkflowTaskState
{
    Open = 0,
    Done = 1,
    Escalated = 2,
    Cancelled = 3
}
