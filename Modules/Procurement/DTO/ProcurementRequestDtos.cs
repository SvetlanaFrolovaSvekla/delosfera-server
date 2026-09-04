using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.DTO;

/// <summary>Строка реестра «Заявки и закупки».</summary>
public class ProcurementListItemDto
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? RegNumber { get; set; }
    public required string Subject { get; set; }
    public required string StatusCode { get; set; }
    public required string MethodShortTitle { get; set; }
    public decimal Amount { get; set; }
    public bool IsAffiliated { get; set; }
    public bool HasBudget { get; set; }
    public string? InitiatorName { get; set; }
    public string? InitiatorUnit { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Заявка пришла из служебной записки — в реестре видно исходный документ.</summary>
    public string? SourceSzRegNumber { get; set; }
}

/// <summary>Фильтр реестра закупок.</summary>
public class ProcurementSearchRequest
{
    public string? Query { get; set; }
    public List<string>? Statuses { get; set; }
    public int? MethodId { get; set; }
    public bool? MineOnly { get; set; }
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Счётчики вкладок реестра.</summary>
public class ProcurementCountersDto
{
    public int All { get; set; }
    public int Drafts { get; set; }
    public int OnApproval { get; set; }
    public int InProcurement { get; set; }
    public int Completed { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = [];
}

/// <summary>Создание заявки мастером (PRC-01).</summary>
/// <summary>
/// Похожая закупка того же подразделения за последние два месяца (п. 10.3).
/// Признак возможного дробления закупки.
/// </summary>
public class SimilarRequestDto
{
    public int Id { get; set; }
    public string? RegNumber { get; set; }
    public required string Subject { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string StatusCode { get; set; }
}

public class ProcurementCreateRequest
{
    public required string Subject { get; set; }
    public string? Justification { get; set; }
    public ProcurementSubjectKind SubjectKind { get; set; } = ProcurementSubjectKind.Goods;
    public decimal Amount { get; set; }
    public bool IsAffiliated { get; set; }
    public bool HasBudget { get; set; }

    /// <summary>Выбранная позиция Плана закупок; пусто — закупка внеплановая.</summary>
    public int? PlanItemId { get; set; }

    public bool HasSpecification { get; set; }

    /// <summary>Вложение с техническим заданием — из вложений этой же заявки.</summary>
    public int? SpecificationAttachmentId { get; set; }

    /// <summary>Желаемое окно объявления закупки: «с» и «по».</summary>
    public DateOnly? AnnouncementFrom { get; set; }
    public DateOnly? AnnouncementTo { get; set; }

    public int? InitiatorUnitId { get; set; }
    public int? CuratorUserId { get; set; }

    /// <summary>Способ, выбранный инициатором; пусто — подбирает Матрица полномочий.</summary>
    public string? PreferredMethod { get; set; }

    /// <summary>Обоснование способа — обязательно для прямого заключения.</summary>
    public string? MethodJustification { get; set; }

    /// <summary>
    /// Заявка создаётся по служебной записке: документ-заготовка уже заведён
    /// SzProcurementService, и заполняются только закупочные поля.
    /// </summary>
    public int? ExistingDocumentId { get; set; }
}

/// <summary>Карточка заявки на закупку.</summary>
public class ProcurementCardDto
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? RegNumber { get; set; }
    public required string Subject { get; set; }
    public required string StatusCode { get; set; }
    public string? Justification { get; set; }

    public ProcurementSubjectKind SubjectKind { get; set; }
    public required string SubjectKindTitle { get; set; }

    public decimal Amount { get; set; }
    public bool IsAffiliated { get; set; }
    public bool HasBudget { get; set; }

    /// <summary>
    /// Идентификаторы для правки: экран правки заполняет ими форму, а показывает
    /// человеку названия — они рядом.
    /// </summary>
    public int? InitiatorUnitId { get; set; }
    public int? CuratorUserId { get; set; }

    /// <summary>Приложенное техническое задание.</summary>
    public int? SpecificationAttachmentId { get; set; }
    public string? SpecificationFileName { get; set; }

    /// <summary>Позиция Плана: ссылка и её код с предметом для показа.</summary>
    public int? PlanItemId { get; set; }
    public string? PlanItem { get; set; }

    public bool HasSpecification { get; set; }

    /// <summary>Желаемое окно объявления закупки: «с» и «по».</summary>
    public DateOnly? AnnouncementFrom { get; set; }
    public DateOnly? AnnouncementTo { get; set; }

    public string? InitiatorName { get; set; }
    public string? InitiatorUnit { get; set; }
    public string? CuratorName { get; set; }

    public required string MethodTitle { get; set; }
    public required string MethodShortTitle { get; set; }
    public string? MethodJustification { get; set; }
    public string? ApprovalChain { get; set; }
    public required string ApprovalAuthorityTitle { get; set; }
    public bool ProtocolRequired { get; set; }
    public int MinProposals { get; set; }

    /// <summary>
    /// Нужен ли по этой закупке договор (раздел VII Положения) и почему.
    ///
    /// Решение показывается на карточке: закупка на сорок тысяч сом договора не
    /// требует, и сотрудник не должен выяснять это по памяти — как и обратное,
    /// когда договор нужен несмотря на малую сумму.
    /// </summary>
    public bool ContractRequired { get; set; }
    public string? ContractRequirementReason { get; set; }

    public string? SourceSzRegNumber { get; set; }
    public int? SourceSzId { get; set; }

    /// <summary>
    /// Рассмотрение на коллегиальном органе: заседание, номер вопроса, решение.
    /// Пусто — заявка на орган не выносилась.
    /// </summary>
    public Meetings.DTO.BoardReviewDto? BoardReview { get; set; }

    /// <summary>Запущенный маршрут согласования (PRC-08), если заявка отправлена.</summary>
    public int? RouteInstanceId { get; set; }

    /// <summary>Действующий конкурс по закупке (PRC-13) — к нему относится ГОКЗ.</summary>
    public int? TenderId { get; set; }

    /// <summary>Действующий договор (PRC-18) — к нему относятся ГОИД и претензии.</summary>
    public int? ContractId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Чего не хватает, чтобы двигать заявку дальше.</summary>
    public List<string> Blockers { get; set; } = [];

    /// <summary>
    /// Похожие закупки подразделения за два месяца. Не блокируют: решение о
    /// консолидации принимает организатор закупок (п. 10.3 Положения).
    /// </summary>
    public List<SimilarRequestDto> SimilarRequests { get; set; } = [];
}
