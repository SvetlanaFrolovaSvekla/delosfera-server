using delosfera_server.Modules.RiskEvents.Models;

namespace delosfera_server.Modules.RiskEvents.DTO;

/// <summary>
/// Поднять событие операционного риска. Источник задаётся любым из полей привязки:
/// с шага согласования достаточно RouteParticipantId (документ и маршрут выводятся
/// автоматически), с карточки документа — DocumentId.
/// </summary>
public class RaiseRiskEventRequest
{
    public RiskEventType Type { get; set; } = RiskEventType.ExecutionDeliveryProcessManagement;
    public RiskEventSeverity Severity { get; set; } = RiskEventSeverity.Medium;

    public string Description { get; set; } = "";

    // ── Привязка к источнику (любое из полей) ─────────────────────────────────
    public int? DocumentId { get; set; }
    public int? RouteInstanceId { get; set; }
    public int? RouteStepId { get; set; }
    public int? RouteParticipantId { get; set; }

    // ── Прямые потери ─────────────────────────────────────────────────────────
    public decimal? DirectLossAmount { get; set; }
    public string? LossCurrency { get; set; }

    /// <summary>Дата события; по умолчанию — сегодня по времени банка.</summary>
    public DateOnly? OccurredOn { get; set; }
}

/// <summary>Смена статуса события и/или фиксация суммы прямых потерь.</summary>
public class ChangeRiskEventStatusRequest
{
    public RiskEventStatus Status { get; set; }
    public string? Comment { get; set; }

    /// <summary>Уточнённая сумма прямых потерь (по факту разбора); null — не менять.</summary>
    public decimal? DirectLossAmount { get; set; }
    public string? LossCurrency { get; set; }
}

/// <summary>Фильтр реестра событий операционного риска.</summary>
public class RiskEventFilter
{
    public RiskEventType? Type { get; set; }
    public RiskEventSeverity? Severity { get; set; }
    public RiskEventStatus? Status { get; set; }
    public int? DocumentId { get; set; }
    public int? InitiatorUserId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Строка реестра событий операционного риска.</summary>
public class RiskEventListItem
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string TypeTitle { get; set; } = "";
    public string Severity { get; set; } = "";
    public string SeverityTitle { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusTitle { get; set; } = "";
    public DateOnly OccurredOn { get; set; }
    public string ShortDescription { get; set; } = "";
    public decimal? DirectLossAmount { get; set; }
    public string LossCurrency { get; set; } = "";
    public int? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public int InitiatorUserId { get; set; }
    public string? InitiatorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Карточка события операционного риска.</summary>
public class RiskEventDetails : RiskEventListItem
{
    public string Description { get; set; } = "";
    public string? ResolutionComment { get; set; }
    public int? RouteInstanceId { get; set; }
    public int? RouteStepId { get; set; }
    public int? RouteParticipantId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RiskEventPage
{
    public List<RiskEventListItem> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
