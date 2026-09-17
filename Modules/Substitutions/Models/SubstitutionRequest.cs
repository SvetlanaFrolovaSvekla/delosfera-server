using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Substitutions.Models;

/// <summary>Причина замещения (КСЗ-ЗМ-03).</summary>
public enum SubstitutionReason
{
    Sick = 1,       // Больничный
    Vacation = 2,   // Отпуск
    Dismissal = 3,  // Увольнение
    Other = 9,      // Другое
}

/// <summary>Момент приёма-передачи дел (КСЗ-ЗМ-27).</summary>
public enum HandoverMoment
{
    EndOfDay = 1,   // на конец рабочего дня
    StartOfDay = 2, // на начало рабочего дня
}

/// <summary>Стадия заявки на замещение.</summary>
public enum SubstitutionStatus
{
    Draft = 0,        // Черновик — правит инициатор
    OnApproval = 1,   // На согласовании
    OnExecution = 2,  // На исполнении в УЧР (приказ на время замещения)
    Executed = 3,     // Исполнено
    Rejected = 4,     // Отклонено
    Withdrawn = 5,    // Отозвано инициатором
}

/// <summary>
/// Заявка на замещение (КСЗ-В9, ТР-КСЗ-001 §5.4). Оформляется на время отсутствия
/// работника: комиссия принимает-передаёт дела, УЧР готовит приказ. Адрес филиала
/// (КСЗ-ЗМ-08) в этой версии не заводится — по решению.
/// </summary>
public class SubstitutionRequest : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Регистрационный номер — присваивается при отправке.</summary>
    public string? RegNumber { get; set; }
    public int Year { get; set; }

    public SubstitutionStatus Status { get; set; } = SubstitutionStatus.Draft;

    // ── Блок 1. Общие сведения ──────────────────────────────────────────────
    /// <summary>Инициатор запроса (КСЗ-ЗМ-01).</summary>
    public int InitiatorUserId { get; set; }
    public User? InitiatorUser { get; set; }

    public string Subject { get; set; } = "";                    // КСЗ-ЗМ-02
    public SubstitutionReason Reason { get; set; }               // КСЗ-ЗМ-03

    // ── Блок 2. Отсутствующий сотрудник ─────────────────────────────────────
    public int? AbsentUserId { get; set; }                       // КСЗ-ЗМ-04
    public User? AbsentUser { get; set; }
    public string AbsentName { get; set; } = "";
    public string? AbsentPosition { get; set; }                  // КСЗ-ЗМ-05
    public string? AbsentBranch { get; set; }                    // КСЗ-ЗМ-06
    public int? AbsentUnitId { get; set; }                       // КСЗ-ЗМ-07
    public OrganizationUnit? AbsentUnit { get; set; }

    // ── Блок 3. Замещающий сотрудник ────────────────────────────────────────
    public int? SubstituteUserId { get; set; }                   // КСЗ-ЗМ-09
    public User? SubstituteUser { get; set; }
    public string SubstituteName { get; set; } = "";
    public string? SubstitutePosition { get; set; }              // КСЗ-ЗМ-10
    public string? SubstituteBranch { get; set; }                // КСЗ-ЗМ-11
    public int? SubstituteUnitId { get; set; }                   // КСЗ-ЗМ-12
    public OrganizationUnit? SubstituteUnit { get; set; }
    public string? PassportSeriesNumber { get; set; }            // КСЗ-ЗМ-13
    public string? PassportIssuedBy { get; set; }                // КСЗ-ЗМ-14
    public string? Inn { get; set; }                             // КСЗ-ЗМ-15 (14 знаков)
    public DateOnly? PassportIssuedOn { get; set; }              // КСЗ-ЗМ-16
    public DateOnly? PassportValidUntil { get; set; }            // КСЗ-ЗМ-17
    public string? AddressRegistration { get; set; }             // КСЗ-ЗМ-18
    public string? AddressResidence { get; set; }                // КСЗ-ЗМ-19

    // ── Блок 4. Период замещения ────────────────────────────────────────────
    public int? DaysCount { get; set; }                          // КСЗ-ЗМ-20
    public DateOnly? StartsOn { get; set; }                      // КСЗ-ЗМ-21
    public DateOnly? EndsOn { get; set; }                        // КСЗ-ЗМ-22

    // ── Блок 5. Комиссия приёма-передачи ────────────────────────────────────
    public int? CommissionChairUserId { get; set; }             // КСЗ-ЗМ-23
    public User? CommissionChairUser { get; set; }
    public string? CommissionChairName { get; set; }
    public string? CommissionChairPosition { get; set; }         // КСЗ-ЗМ-24
    public HandoverMoment HandoverMoment { get; set; } = HandoverMoment.EndOfDay; // КСЗ-ЗМ-27
    public DateOnly? HandoverOn { get; set; }                    // КСЗ-ЗМ-28

    public ICollection<SubstitutionCommissionMember> CommissionMembers { get; set; }
        = new List<SubstitutionCommissionMember>();

    /// <summary>Этапы маршрута согласования (директор филиала → Опер. управление → УЧР).</summary>
    public ICollection<SubstitutionApproval> Approvals { get; set; }
        = new List<SubstitutionApproval>();

    // ── Блок 6. Прочее ──────────────────────────────────────────────────────
    public string? Description { get; set; }                     // КСЗ-ЗМ-29

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Член комиссии приёма-передачи (КСЗ-ЗМ-25/26); их может быть несколько.</summary>
public class SubstitutionCommissionMember
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public SubstitutionRequest? Request { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }
    public string FullName { get; set; } = "";
    public string? Position { get; set; }
    public int SortOrder { get; set; }
}
