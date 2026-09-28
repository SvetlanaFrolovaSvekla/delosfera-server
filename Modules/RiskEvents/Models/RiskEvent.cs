using delosfera_server.Common.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.RiskEvents.Models;

/// <summary>
/// Тип события операционного риска (Положение Банка России 716-П, приложение 3 /
/// Базель II). Семь классификационных категорий по источнику потерь — единая шкала,
/// на которой регулятор ждёт учёта событий, а банк собирает базу для расчёта капитала
/// под операционный риск.
/// </summary>
public enum RiskEventType
{
    /// <summary>Внутреннее мошенничество: действия сотрудников в обход правил и контролей.</summary>
    InternalFraud = 1,

    /// <summary>Внешнее мошенничество: действия третьих лиц (подлог, кибератака, кража).</summary>
    ExternalFraud = 2,

    /// <summary>Кадровая политика и безопасность труда: трудовые споры, охрана труда.</summary>
    EmploymentPractices = 3,

    /// <summary>Клиенты, продукты и деловая практика: нарушение обязательств перед
    /// клиентами, ненадлежащая деловая практика, комплаенс-нарушения.</summary>
    ClientsProductsBusinessPractices = 4,

    /// <summary>Ущерб материальным активам: порча или утрата имущества банка.</summary>
    DamageToPhysicalAssets = 5,

    /// <summary>Нарушение деятельности и сбои систем: простои, отказы ИТ-инфраструктуры.</summary>
    BusinessDisruptionSystemFailures = 6,

    /// <summary>Управление процессами, исполнение и поставка: ошибки исполнения операций,
    /// нарушение сроков этапов, срыв контрольных процедур, документооборот.</summary>
    ExecutionDeliveryProcessManagement = 7,
}

/// <summary>Серьёзность события операционного риска (для приоритизации разбора).</summary>
public enum RiskEventSeverity
{
    Low = 1,       // Незначительная
    Medium = 2,    // Умеренная
    High = 3,      // Существенная
    Critical = 4,  // Критическая
}

/// <summary>Стадия разбора события операционного риска.</summary>
public enum RiskEventStatus
{
    New = 0,         // Новое — зарегистрировано, разбор не начат
    InProgress = 1,  // В работе — разбирается риск-подразделением
    Closed = 2,      // Закрыто — разбор завершён, меры приняты
}

/// <summary>
/// Событие операционного риска (RISK-1, 716-П / НБКР). Поднимается с любого шага
/// согласования или карточки документа: просрочка этапа, срыв контроля, обход
/// «четырёх глаз» и прочее. Источник фиксируется ссылками на документ и на шаг
/// маршрута, чтобы событие можно было привязать к конкретному месту в процессе.
///
/// Событие — регулятивная запись: при удалении документа или маршрута оно должно
/// остаться (ссылки обнуляются, но факт и сумма потерь сохраняются).
/// </summary>
public class RiskEvent : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Тип события по классификации 716-П.</summary>
    public RiskEventType Type { get; set; }

    /// <summary>Серьёзность события.</summary>
    public RiskEventSeverity Severity { get; set; } = RiskEventSeverity.Medium;

    /// <summary>Стадия разбора.</summary>
    public RiskEventStatus Status { get; set; } = RiskEventStatus.New;

    // ── Источник: где в процессе возникло событие ─────────────────────────────
    /// <summary>Документ единого реестра (Documents.Document), с которого поднято событие.</summary>
    public int? DocumentId { get; set; }
    public Document? Document { get; set; }

    /// <summary>Экземпляр маршрута согласования (Workflow.RouteInstance).</summary>
    public int? RouteInstanceId { get; set; }
    public RouteInstance? RouteInstance { get; set; }

    /// <summary>Этап маршрута (Workflow.RouteStep), на котором возникло событие.</summary>
    public int? RouteStepId { get; set; }
    public RouteStep? RouteStep { get; set; }

    /// <summary>Участник этапа (Workflow.RouteParticipant) — конкретная задача-источник.</summary>
    public int? RouteParticipantId { get; set; }
    public RouteParticipant? RouteParticipant { get; set; }

    // ── Кто и когда ───────────────────────────────────────────────────────────
    /// <summary>Инициатор события (кто поднял оп-риск).</summary>
    public int InitiatorUserId { get; set; }
    public User? InitiatorUser { get; set; }

    /// <summary>Дата события по времени банка (IBankClock, CLK-1).</summary>
    public DateOnly OccurredOn { get; set; }

    /// <summary>Описание обстоятельств (просрочка этапа, срыв контроля, обход «4 глаз» и т.п.).</summary>
    public string Description { get; set; } = "";

    // ── Прямые потери ─────────────────────────────────────────────────────────
    /// <summary>Сумма прямых потерь; null — потери не оценены/отсутствуют.</summary>
    public decimal? DirectLossAmount { get; set; }

    /// <summary>Валюта прямых потерь (ISO, по умолчанию сом — KGS).</summary>
    public string LossCurrency { get; set; } = "KGS";

    // ── Разбор ────────────────────────────────────────────────────────────────
    /// <summary>Комментарий/решение при смене статуса (мера, причина закрытия).</summary>
    public string? ResolutionComment { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
