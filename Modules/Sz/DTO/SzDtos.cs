using System.Text.Json;
using delosfera_server.Modules.Sz.Models;

using delosfera_server.Modules.Meetings.Models;

namespace delosfera_server.Modules.Sz.DTO;

/// <summary>Создание/правка черновика служебной записки.</summary>
public class SzSaveRequest
{
    public required string Title { get; set; }
    public int KindId { get; set; }
    public string? Body { get; set; }

    public int? CorrespondentUnitId { get; set; }

    /// <summary>«Кому» — пользователь, который выносит решение по записке.</summary>
    public int? AddresseeUserId { get; set; }

    /// <summary>Согласующие в порядке прохождения.</summary>
    public List<int> ApproverUserIds { get; set; } = [];

    /// <summary>Кого автор предлагает в исполнители — подсказка адресату.</summary>
    public List<int> ProposedAssigneeUserIds { get; set; } = [];

    /// <summary>Согласование параллельное; иначе — по очереди.</summary>
    public bool ApprovalIsParallel { get; set; }

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

    /// <summary>
    /// Сотрудники, которых касается кадровая записка. Пустой список — записка
    /// не о людях либо заведена до появления этого поля.
    /// </summary>
    public List<SzEmployeeDto> Employees { get; set; } = [];
}

/// <summary>Согласующий в карточке записки.</summary>
public class SzApproverDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public int Order { get; set; }
}

/// <summary>Решение адресата по существу вопроса.</summary>
public class SzAddresseeDecisionRequest
{
    public required string Decision { get; set; }

    /// <summary>
    /// Поручения, выдаваемые тем же решением. Решение без поручений допустимо —
    /// адресат может ответить по существу, никому ничего не поручая.
    /// </summary>
    public List<SzAssignmentRequest> Assignments { get; set; } = [];
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

    public int? AddresseeUserId { get; set; }
    public string? AddresseeUser { get; set; }

    /// <summary>Кого автор предложил в исполнители — подсказка адресату.</summary>
    public List<SzApproverDto> ProposedAssignees { get; set; } = [];

    /// <summary>Согласующие в порядке прохождения маршрута.</summary>
    public List<SzApproverDto> Approvers { get; set; } = [];
    public bool ApprovalIsParallel { get; set; }

    /// <summary>Решение адресата. Заполняет только он сам.</summary>
    public string? AddresseeDecision { get; set; }
    public DateTime? AddresseeDecisionAt { get; set; }

    public int? SignerUserId { get; set; }
    public string? SignerUser { get; set; }

    /// <summary>
    /// Отметка «вынести на коллегиальный орган»: заявка секретарю, а не
    /// распоряжение. Пусто — записка решается в рабочем порядке.
    /// </summary>
    public string? SubmitToBody { get; set; }
    public string? SubmitToBodyQuestion { get; set; }
    public DateTime? SubmitToBodyRequestedAt { get; set; }

    /// <summary>Включена ли записка в повестку — тогда отметку менять поздно.</summary>
    public bool InAgenda { get; set; }

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

    /// <summary>
    /// Сотрудники, которых касается кадровая записка. Пустой список — записка
    /// не о людях либо заведена до появления этого поля.
    /// </summary>
    public List<SzEmployeeDto> Employees { get; set; } = [];
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

// ── Статистика по служебным запискам (SZ-06) ─────────────────────────────────

public class SzStatisticsFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? OrgUnitId { get; set; }
    public int? KindId { get; set; }
}

/// <summary>Разрез сводки: сколько записок в каждом состоянии.</summary>
public class SzStatisticsCell
{
    public int Total { get; set; }
    public int InWork { get; set; }
    public int Overdue { get; set; }
    public int Executed { get; set; }
    public int Other { get; set; }
}

public class SzStatisticsDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public int Total { get; set; }
    public int InWork { get; set; }
    public int Overdue { get; set; }
    public int Executed { get; set; }

    /// <summary>Черновики, отозванные и забракованные — в работу не считаются.</summary>
    public int Other { get; set; }

    public Dictionary<string, SzStatisticsCell> ByUnit { get; set; } = [];
    public Dictionary<string, SzStatisticsCell> ByKind { get; set; } = [];
    public Dictionary<string, SzStatisticsCell> ByMonth { get; set; } = [];
}


/// <summary>
/// Сотрудник в кадровой записке. UserId пуст у кандидата: в записке о приёме
/// человека в системе ещё нет, а записка уже нужна.
/// </summary>
public class SzEmployeeDto
{
    public int? Id { get; set; }
    public int? UserId { get; set; }
    public required string FullName { get; set; }
    public int? OrgUnitId { get; set; }
    public string? OrgUnit { get; set; }
    public string? Position { get; set; }

    /// <summary>Значения полей, своих для этого человека: оклад, даты.</summary>
    public JsonElement? Values { get; set; }
}

/// <summary>Ручной перевод записки в другой статус администратором.</summary>
public class SzForceStatusRequest
{
    public required string StatusCode { get; set; }

    /// <summary>Основание перевода — попадает в журнал действий.</summary>
    public required string Reason { get; set; }
}



/// <summary>Вынесение вопроса по записке на коллегиальный орган.</summary>
public class SzToBodyRequest
{
    public MeetingBody Body { get; set; }

    /// <summary>
    /// Формулировка вопроса для повестки. Тема записки и вопрос заседания —
    /// разные тексты; пусто — секретарь возьмёт тему записки.
    /// </summary>
    public string? Question { get; set; }
}
