using System.Text.Json;
using delosfera_server.Modules.Sz.Models;

namespace delosfera_server.Modules.Sz.DTO;

/// <summary>Создание/правка черновика служебной записки.</summary>
public class SzSaveRequest
{
    public required string Title { get; set; }
    public int KindId { get; set; }
    public string? Body { get; set; }

    public int? CorrespondentUnitId { get; set; }
    public int? SignerUserId { get; set; }
    public bool? IsPaperCarrier { get; set; }
    public List<int> RubricIds { get; set; } = [];

    // Поля кадровых СЗ
    public int? HrKindId { get; set; }
    public string? EmployeeName { get; set; }
    public int? EmployeeUnitId { get; set; }
    public int? TransferUnitId { get; set; }

    // Поля СЗ на закупку
    public bool? HasBudget { get; set; }
    public decimal? Amount { get; set; }

    // Поля СЗ на обучение
    public bool? TravelExpenses { get; set; }

    /// <summary>Поля видов, добавленных администратором после релиза.</summary>
    public JsonElement? ExtraFields { get; set; }
}

/// <summary>Фильтры реестра СЗ.</summary>
public class SzSearchRequest
{
    /// <summary>Поиск по номеру, заголовку и тексту.</summary>
    public string? Query { get; set; }

    /// <summary>Коды статусов; пусто — неархивные (реестр по умолчанию).</summary>
    public List<string> Statuses { get; set; } = [];

    public List<int> KindIds { get; set; } = [];
    public int? AuthorId { get; set; }
    public int? CorrespondentUnitId { get; set; }
    public int? RubricId { get; set; }

    /// <summary>Только записки текущего пользователя («СЗ, инициирую я» + черновики).</summary>
    public bool MineOnly { get; set; }

    public DateOnly? RegisteredFrom { get; set; }
    public DateOnly? RegisteredTo { get; set; }

    /// <summary>Только просроченные по сроку исполнения.</summary>
    public bool OverdueOnly { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

/// <summary>Строка реестра СЗ.</summary>
public class SzListItem
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? RegNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public int KindId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? CorrespondentUnit { get; set; }
    public DateOnly? RegisteredOn { get; set; }
    public DateOnly? DueDate { get; set; }
    public int? DaysLeft { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsPaperCarrier { get; set; }
}

/// <summary>Карточка СЗ целиком.</summary>
public class SzDetails : SzListItem
{
    public SzFormKey FormKey { get; set; }
    public string? Body { get; set; }
    public int AuthorId { get; set; }
    public int? AuthorUnitId { get; set; }
    public string? AuthorUnit { get; set; }
    public int? CorrespondentUnitId { get; set; }
    public int? SignerUserId { get; set; }
    public string? SignerUser { get; set; }
    public int? RegisteredByUserId { get; set; }
    public string? RegisteredBy { get; set; }
    public List<int> RubricIds { get; set; } = [];
    public List<string> Rubrics { get; set; } = [];

    public int? HrKindId { get; set; }
    public string? HrKind { get; set; }
    public string? EmployeeName { get; set; }
    public int? EmployeeUnitId { get; set; }
    public string? EmployeeUnit { get; set; }
    public int? TransferUnitId { get; set; }
    public string? TransferUnit { get; set; }

    public bool? HasBudget { get; set; }
    public decimal? Amount { get; set; }
    public bool? TravelExpenses { get; set; }

    public JsonElement? ExtraFields { get; set; }
    public int? CurrentRouteInstanceId { get; set; }

    /// <summary>Обоснование последнего отзыва — участники должны видеть, почему процесс прерван.</summary>
    public string? WithdrawReason { get; set; }

    /// <summary>Сколько раз записка уходила на согласование.</summary>
    public int ApprovalRounds { get; set; }

    /// <summary>Резолюция руководителя, по которой выданы поручения.</summary>
    public string? ExecutionResolution { get; set; }
    public DateTime? ExecutionResolutionAt { get; set; }

    /// <summary>Обоснование последнего продления срока и число продлений.</summary>
    public string? DueDateExtensionReason { get; set; }
    public int DueDateExtensions { get; set; }

    /// <summary>Итог исполнения.</summary>
    public string? ExecutionSummary { get; set; }
    public DateTime? ExecutedAt { get; set; }
}
