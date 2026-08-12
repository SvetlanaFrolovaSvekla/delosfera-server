using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.DTO;

// ── План и позиции ───────────────────────────────────────────────────────────

public class PlanCreateRequest
{
    public int Year { get; set; }
}

public class PlanApproveRequest
{
    public required string ApprovalNote { get; set; }
}

public class PlanItemSaveRequest
{
    public required string Title { get; set; }
    public int? VndDocumentId { get; set; }
    public int? ResponsibleUnitId { get; set; }
    public int? CuratorUserId { get; set; }
    public int? ApprovalBodyId { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Перенос срока: причина обязательна — план утверждён, и правка должна быть объяснена.</summary>
public class PlanItemRescheduleRequest
{
    public DateOnly DueDate { get; set; }
    public required string Reason { get; set; }
}

public class PlanItemExcludeRequest
{
    public required string Reason { get; set; }
}

/// <summary>Цвет позиции в дашборде методологии (PLN-03).</summary>
public enum PlanItemUrgency
{
    Green = 1,
    Yellow = 2,
    Red = 3,

    /// <summary>Работа завершена — в светофор не попадает.</summary>
    Done = 4,
}

public class PlanItemDto
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public int Order { get; set; }

    public string Title { get; set; } = string.Empty;
    public int? VndDocumentId { get; set; }
    public string? VndCode { get; set; }

    public int? ResponsibleUnitId { get; set; }
    public string? ResponsibleUnitTitle { get; set; }
    public int? CuratorUserId { get; set; }
    public string? CuratorName { get; set; }
    public int? ApprovalBodyId { get; set; }
    public string? ApprovalBodyTitle { get; set; }

    public DateOnly DueDate { get; set; }
    public DateOnly? NextDueDate { get; set; }
    public DateOnly? StartedOn { get; set; }
    public DateOnly? CompletedOn { get; set; }

    public PlanItemStatus Status { get; set; }
    public string StatusTitle { get; set; } = string.Empty;

    public PlanItemUrgency Urgency { get; set; }

    /// <summary>Дней до срока; отрицательное — просрочка.</summary>
    public int DaysLeft { get; set; }

    public string? Comment { get; set; }

    /// <summary>Позиция не сопоставлена с базой ВНД — по ней нельзя запустить актуализацию.</summary>
    public bool IsUnmatched { get; set; }
}

public class PlanDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public ActualizationPlanStatus Status { get; set; }
    public string StatusTitle { get; set; } = string.Empty;
    public string? ApprovalNote { get; set; }
    public DateOnly? ApprovedOn { get; set; }

    public List<PlanItemDto> Items { get; set; } = [];

    public int Total { get; set; }
    public int Green { get; set; }
    public int Yellow { get; set; }
    public int Red { get; set; }
    public int Done { get; set; }

    /// <summary>Позиции, не сопоставленные с базой ВНД: их надо привязать руками.</summary>
    public int Unmatched { get; set; }
}

public class PlanItemEventDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public DateTime At { get; set; }
}

// ── Импорт ───────────────────────────────────────────────────────────────────

public class PlanImportResultDto
{
    public int PlanId { get; set; }
    public int Year { get; set; }

    public int RowsRead { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Matched { get; set; }

    /// <summary>Строки, которые импорт не принял, с причиной.</summary>
    public List<string> Skipped { get; set; } = [];

    /// <summary>Позиции, для которых не нашёлся документ в базе ВНД.</summary>
    public List<string> Unmatched { get; set; } = [];
}

// ── Настройки ────────────────────────────────────────────────────────────────

public class ActualizationSettingsDto
{
    public int GreenThresholdDays { get; set; }
    public int RedThresholdDays { get; set; }
    public int CriticalReminderDays { get; set; }
    public bool MonthlyDigestEnabled { get; set; }
}
