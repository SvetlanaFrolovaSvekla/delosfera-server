using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.DTO;
using delosfera_server.Modules.Analytics.DTO.Response;
using delosfera_server.Modules.Correspondence.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Hr.Models;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Analytics.Services;

public interface IContourReportsService
{
    Task<ContourReportDto> ProcurementAsync();
    Task<ContourReportDto> MeetingsAsync();
    Task<ContourReportDto> HrAsync();
    Task<ContourReportDto> OfficeAsync();
}

/// <summary>
/// Отчёты по контурам (АН-1..4): закупки, заседания, кадровый ДО, канцелярия. Единый
/// формат — KPI-плашки плюс распределения; берётся за текущий год.
/// </summary>
public class ContourReportsService : IContourReportsService
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public ContourReportsService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // ── АН-1: закупки ──
    public async Task<ContourReportDto> ProcurementAsync()
    {
        var year = _clock.Today.Year;
        var reqs = await _db.ProcurementRequests
            .Where(r => r.CreatedAt.Year == year)
            .Select(r => new {r.Amount, Status = r.Document!.StatusCode, r.SubjectKind})
            .ToListAsync();

        return new ContourReportDto
        {
            Kpis =
            [
                Kpi("Заявок за год", reqs.Count),
                Kpi("В процедуре", reqs.Count(r => r.Status == ProcurementStatus.InProcurement)),
                Kpi("Завершено", reqs.Count(r => r.Status == ProcurementStatus.Completed)),
                Money("Сумма заявок", reqs.Sum(r => r.Amount)),
            ],
            Charts =
            [
                Chart("По стадиям", Dist(reqs, r => ProcStatusTitle(r.Status))),
                Chart("По предмету закупки", Dist(reqs, r => r.SubjectKind.ToString())),
            ],
        };
    }

    // ── АН-2: заседания ──
    public async Task<ContourReportDto> MeetingsAsync()
    {
        var year = _clock.Today.Year;
        var today = _clock.Today;

        var meetings = await _db.Meetings
            .Where(m => m.Date.Year == year)
            .Select(m => new {m.Id, m.Body})
            .ToListAsync();
        var meetIds = meetings.Select(m => m.Id).ToHashSet();

        // Вопросы повестки заседаний года; поручения по ним лежат отдельной таблицей
        // (AgendaAssignment) — со сроком и статусом исполнения.
        var itemIds = await _db.AgendaItems
            .Where(a => meetIds.Contains(a.MeetingId))
            .Select(a => a.Id)
            .ToListAsync();
        var itemIdSet = itemIds.ToHashSet();

        var assignments = await _db.AgendaAssignments
            .Where(a => itemIdSet.Contains(a.AgendaItemId))
            .Select(a => new {a.Status, a.DueDate})
            .ToListAsync();

        var done = new[] {ExecutionStatus.DoneOnTime, ExecutionStatus.DoneLate, ExecutionStatus.Cancelled, ExecutionStatus.Excluded};
        var overdue = assignments.Count(i => i.DueDate is { } d && d < today && !done.Contains(i.Status));

        return new ContourReportDto
        {
            Kpis =
            [
                Kpi("Заседаний за год", meetings.Count),
                Kpi("Вопросов повестки", itemIds.Count),
                Kpi("Поручений", assignments.Count),
                Kpi("Просрочено поручений", overdue, overdue > 0 ? "danger" : "normal"),
            ],
            Charts =
            [
                Chart("По органам", Dist(meetings, m => BodyTitle(m.Body))),
                Chart("Исполнение поручений", Dist(assignments, i => ExecTitle(i.Status))),
            ],
        };
    }

    // ── АН-3: кадровый ДО ──
    public async Task<ContourReportDto> HrAsync()
    {
        var year = _clock.Today.Year;

        var orders = await _db.HrOrders
            .Where(o => o.Year == year)
            .Select(o => new {o.Kind, o.Status})
            .ToListAsync();

        var sheets = await _db.AcknowledgementSheets.CountAsync(s => s.CreatedAt.Year == year);
        var pending = await _db.AcknowledgementEntries
            .CountAsync(e => e.State == AcknowledgementState.Pending && e.Sheet!.ClosedAt == null);

        return new ContourReportDto
        {
            Kpis =
            [
                Kpi("Приказов за год", orders.Count),
                Kpi("Подписано", orders.Count(o => o.Status == HrOrderStatus.Signed)),
                Kpi("Листов ознакомления", sheets),
                Kpi("Ждут ознакомления", pending, pending > 0 ? "warning" : "normal"),
            ],
            Charts =
            [
                Chart("По видам приказов", Dist(orders, o => HrKindTitle(o.Kind))),
                Chart("По статусам", Dist(orders, o => HrStatusTitle(o.Status))),
            ],
        };
    }

    // ── АН-4: канцелярия ──
    public async Task<ContourReportDto> OfficeAsync()
    {
        var year = _clock.Today.Year;

        var letters = await _db.CorrespondenceLetters
            .Where(l => l.RegisteredOn != null && l.RegisteredOn.Value.Year == year)
            .Select(l => new {l.Direction, l.Status})
            .ToListAsync();

        var poa = await _db.PowersOfAttorney.CountAsync();

        return new ContourReportDto
        {
            Kpis =
            [
                Kpi("Писем за год", letters.Count),
                Kpi("Входящих", letters.Count(l => l.Direction == LetterDirection.Incoming)),
                Kpi("Исходящих", letters.Count(l => l.Direction == LetterDirection.Outgoing)),
                Kpi("Доверенностей", poa),
            ],
            Charts =
            [
                Chart("По направлению", Dist(letters, l => DirTitle(l.Direction))),
                Chart("По статусам писем", Dist(letters, l => LetterStatusTitle(l.Status))),
            ],
        };
    }

    // ── helpers ──
    private static ReportKpiDto Kpi(string label, int value, string tone = "normal") =>
        new() {Label = label, Value = value.ToString(), Tone = tone};

    private static ReportKpiDto Money(string label, decimal amount) =>
        new() {Label = label, Value = $"{amount:N0} сом"};

    private static ReportChartDto Chart(string title, List<ChartCategoryPoint> points) =>
        new() {Title = title, Points = points};

    private static List<ChartCategoryPoint> Dist<T>(IEnumerable<T> items, Func<T, string> label)
    {
        var groups = items.GroupBy(label).Select(g => new {g.Key, Count = g.Count()}).ToList();
        var total = groups.Sum(g => g.Count);
        return groups
            .OrderByDescending(g => g.Count)
            .Select(g => new ChartCategoryPoint
            {
                Label = g.Key,
                Value = g.Count,
                Percent = total == 0 ? 0 : Math.Round(g.Count * 100.0 / total, 1),
            })
            .ToList();
    }

    private static string ProcStatusTitle(string code) => code switch
    {
        ProcurementStatus.Draft => "Черновик",
        ProcurementStatus.OnApproval => "На согласовании",
        ProcurementStatus.Approved => "Согласована",
        ProcurementStatus.InProcurement => "В процедуре",
        ProcurementStatus.Completed => "Завершена",
        ProcurementStatus.OnRevision => "На доработке",
        ProcurementStatus.Rejected => "Отклонена",
        ProcurementStatus.Cancelled => "Отменена",
        _ => code,
    };

    private static string BodyTitle(MeetingBody b) => b switch
    {
        MeetingBody.Board => "Правление",
        MeetingBody.Kpa => "КПА",
        MeetingBody.CreditCommittee => "Кредитный комитет",
        _ => b.ToString(),
    };

    private static string ExecTitle(ExecutionStatus s) => s switch
    {
        ExecutionStatus.New => "Новое",
        ExecutionStatus.InProgress => "В работе",
        ExecutionStatus.DoneOnTime => "Исполнено в срок",
        ExecutionStatus.DoneLate => "Исполнено с опозданием",
        ExecutionStatus.NotDone => "Не исполнено",
        ExecutionStatus.Cancelled => "Отменено",
        ExecutionStatus.Excluded => "Снято",
        _ => s.ToString(),
    };

    private static string HrKindTitle(HrOrderKind k) => k switch
    {
        HrOrderKind.Hiring => "Приём",
        HrOrderKind.Transfer => "Перевод",
        HrOrderKind.Dismissal => "Увольнение",
        HrOrderKind.Leave => "Отпуск",
        HrOrderKind.BusinessTrip => "Командировка",
        HrOrderKind.Salary => "Оклад",
        HrOrderKind.Bonus => "Премия",
        HrOrderKind.Discipline => "Взыскание",
        HrOrderKind.Training => "Обучение",
        HrOrderKind.Combination => "Совмещение",
        _ => "Прочее",
    };

    private static string HrStatusTitle(HrOrderStatus s) => s switch
    {
        HrOrderStatus.Draft => "Черновик",
        HrOrderStatus.OnSigning => "На подписании",
        HrOrderStatus.Signed => "Подписан",
        HrOrderStatus.Cancelled => "Отменён",
        _ => s.ToString(),
    };

    private static string DirTitle(LetterDirection d) =>
        d == LetterDirection.Incoming ? "Входящие" : "Исходящие";

    private static string LetterStatusTitle(LetterStatus s) => s switch
    {
        LetterStatus.Draft => "Черновик",
        LetterStatus.Registered => "Зарегистрировано",
        LetterStatus.OnResolution => "На резолюции",
        LetterStatus.OnExecution => "На исполнении",
        LetterStatus.Answered => "Отвечено",
        LetterStatus.Closed => "Закрыто",
        LetterStatus.Sent => "Отправлено",
        _ => s.ToString(),
    };
}
