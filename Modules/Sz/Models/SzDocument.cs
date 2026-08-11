using System.Text.Json;
using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Models;

/// <summary>
/// Служебная записка (контур СЗ). Номер, статус, автор, вложения и аудит живут в единой
/// карточке документа (Documents), здесь — только контурные поля: адресат, подписант,
/// сроки и реквизиты, зависящие от вида записки.
/// </summary>
public class SzDocument : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Единая карточка документа: номер, статус, автор, вложения, аудит (GEN-05).</summary>
    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int KindId { get; set; }
    public SzKind? Kind { get; set; }

    /// <summary>Текст служебной записки.</summary>
    public string? Body { get; set; }

    /// <summary>СП автора.</summary>
    public int? AuthorUnitId { get; set; }
    public OrganizationUnit? AuthorUnit { get; set; }

    /// <summary>Адресат — структурное подразделение.</summary>
    public int? CorrespondentUnitId { get; set; }
    public OrganizationUnit? CorrespondentUnit { get; set; }

    /// <summary>Подписант (Председатель / Заместитель Председателя Правления).</summary>
    public int? SignerUserId { get; set; }
    public User? SignerUser { get; set; }

    /// <summary>Дата регистрации сектором делопроизводства.</summary>
    public DateOnly? RegisteredOn { get; set; }
    public int? RegisteredByUserId { get; set; }

    /// <summary>Срок исполнения: норматив вида (по умолчанию 14 дней) от даты регистрации.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Обоснование отзыва (SZ): записка возвращается в черновик, но участники согласования
    /// должны видеть, почему процесс прерван.
    /// </summary>
    public string? WithdrawReason { get; set; }

    /// <summary>Сколько раз записка уходила на согласование — повтор идёт с первого этапа.</summary>
    public int ApprovalRounds { get; set; }

    // --- исполнение ---

    /// <summary>Текст резолюции руководителя, по которой выданы поручения.</summary>
    public string? ExecutionResolution { get; set; }
    public int? ExecutionResolutionByUserId { get; set; }
    public DateTime? ExecutionResolutionAt { get; set; }

    /// <summary>Поручения по записке: исполнена, когда все они закрыты.</summary>
    public ICollection<SzAssignment> Assignments { get; set; } = new List<SzAssignment>();

    /// <summary>Обоснование последнего продления срока (норматив 14 дней продлевает СП-исполнитель).</summary>
    public string? DueDateExtensionReason { get; set; }

    /// <summary>Сколько раз срок продлевали — по этому полю видно проблемные записки.</summary>
    public int DueDateExtensions { get; set; }

    /// <summary>Итог исполнения: чем закрыта записка.</summary>
    public string? ExecutionSummary { get; set; }
    public DateTime? ExecutedAt { get; set; }

    // --- бумажный контур (SZ-PAP): контроль оригинала ---

    /// <summary>Кому выдан бумажный оригинал на подписание или ознакомление.</summary>
    public int? OriginalHolderUserId { get; set; }
    public User? OriginalHolderUser { get; set; }

    public DateTime? OriginalHandedAt { get; set; }

    /// <summary>Дата, к которой оригинал ждут обратно — по ней считается просрочка возврата.</summary>
    public DateOnly? OriginalDueBackOn { get; set; }

    /// <summary>Где находится оригинал, пока он на руках.</summary>
    public string? OriginalLocation { get; set; }

    public DateTime? OriginalReturnedAt { get; set; }
    public int? OriginalReturnedToUserId { get; set; }

    /// <summary>Сколько раз оригинал выдавали — записка может ходить по нескольким подписантам.</summary>
    public int OriginalHandoverCount { get; set; }

    /// <summary>Рубрикатор — записка может лежать в нескольких рубриках (мультипапка).</summary>
    public ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();

    // --- поля кадровых СЗ ---

    public int? HrKindId { get; set; }
    public SzHrKind? HrKind { get; set; }

    /// <summary>ФИО сотрудника, которого касается записка.</summary>
    public string? EmployeeName { get; set; }

    /// <summary>Филиал/СП сотрудника.</summary>
    public int? EmployeeUnitId { get; set; }
    public OrganizationUnit? EmployeeUnit { get; set; }

    /// <summary>Филиал/СП перевода (для перемещений).</summary>
    public int? TransferUnitId { get; set; }
    public OrganizationUnit? TransferUnit { get; set; }

    // --- поля СЗ на закупку ---

    /// <summary>Заложено ли в бюджет.</summary>
    public bool? HasBudget { get; set; }

    /// <summary>Сумма закупки: по ней ветвится запуск закупочного контура (PRC-01).</summary>
    public decimal? Amount { get; set; }

    // --- поля СЗ на обучение ---

    /// <summary>Наличие командировочных расходов.</summary>
    public bool? TravelExpenses { get; set; }

    /// <summary>
    /// Поля видов, добавленных администратором после релиза. Типовые реквизиты из ТЗ
    /// лежат отдельными колонками — по ним идут фильтры и отчёты.
    /// </summary>
    public JsonDocument? ExtraFields { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
