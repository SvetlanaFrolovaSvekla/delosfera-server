using System.Globalization;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzStatisticsService
{
    Task<SzStatisticsDto> GetAsync(SzStatisticsFilter filter);
    Task<byte[]> ExportAsync(SzStatisticsFilter filter);
}

/// <summary>
/// Статистика по служебным запискам (SZ-06): в работе, просрочено, исполнено —
/// в разрезе подразделений, видов и периодов.
///
/// «Просрочено» считается по поручениям, а не по самой записке: срок исполнения
/// живёт у поручения, и записка с тремя поручениями бывает просрочена частично.
/// Сводка по такой записке молчала бы, а работа при этом стоит.
/// </summary>
public class SzStatisticsService : ISzStatisticsService
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public SzStatisticsService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<SzStatisticsDto> GetAsync(SzStatisticsFilter filter) =>
        Summarize(filter, await LoadAsync(filter), _clock.Today);

    private static SzStatisticsDto Summarize(SzStatisticsFilter filter, List<SzRow> rows, DateOnly today)
    {
        var dto = new SzStatisticsDto
        {
            From = filter.From,
            To = filter.To,
            Total = rows.Count,
        };

        foreach (var row in rows)
        {
            var state = Classify(row, today);

            Bump(dto.ByUnit, row.UnitTitle ?? "Без подразделения", state);
            Bump(dto.ByKind, row.KindTitle ?? "Без вида", state);
            Bump(dto.ByMonth, row.CreatedAt.ToString("MM.yyyy"), state);

            switch (state)
            {
                case SzProgress.InWork: dto.InWork++; break;
                case SzProgress.Overdue: dto.Overdue++; break;
                case SzProgress.Executed: dto.Executed++; break;
                default: dto.Other++; break;
            }
        }

        return dto;
    }

    public async Task<byte[]> ExportAsync(SzStatisticsFilter filter)
    {
        var today = _clock.Today;
        var rows = await LoadAsync(filter);
        var summary = Summarize(filter, rows, today);

        // Подписи фильтров для шапки отчёта: без них распечатка «ни о чём» - непонятно,
        // за какой период и по какому подразделению/виду эти цифры.
        var unitName = filter.OrgUnitId is { } unitId
            ? await _db.OrganizationUnits.AsNoTracking().Where(u => u.Id == unitId).Select(u => u.TitleRu).FirstOrDefaultAsync()
            : null;
        var kindName = filter.KindId is { } kindId
            ? await _db.SzKinds.AsNoTracking().Where(k => k.Id == kindId).Select(k => k.TitleRu).FirstOrDefaultAsync()
            : null;

        var period = PeriodText(filter.From, filter.To);
        var generated = _clock.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        void Caption(SzStatisticsWorkbook.Sheet sheet, string title, int lastCol)
        {
            sheet.Add(SzStatisticsWorkbook.Cell.Text(title, SzStatisticsWorkbook.STitle));
            sheet.RowHeights[1] = 24;
            sheet.Merges.Add($"A1:{SzStatisticsWorkbook.Col(lastCol)}1");
            sheet.Add(SzStatisticsWorkbook.Cell.Text(
                $"Период: {period}   ·   Подразделение: {unitName ?? "все"}   ·   Вид: {kindName ?? "все"}   ·   Сформировано: {generated}",
                SzStatisticsWorkbook.SSubtitle));
            sheet.Merges.Add($"A2:{SzStatisticsWorkbook.Col(lastCol)}2");
            sheet.AddBlank();
        }

        // ── Сводка ──
        var overview = new SzStatisticsWorkbook.Sheet { Name = "Сводка", Widths = [48, 16, 14] };
        overview.Add(SzStatisticsWorkbook.Cell.Text("Аналитика служебных записок", SzStatisticsWorkbook.STitle));
        overview.RowHeights[1] = 24;
        overview.Merges.Add("A1:C1");
        foreach (var (label, value) in new[]
                 {
                     ("Период", period), ("Подразделение", unitName ?? "все"),
                     ("Вид записки", kindName ?? "все"), ("Сформировано", generated),
                 })
        {
            overview.Add(
                SzStatisticsWorkbook.Cell.Text($"{label}: {value}", SzStatisticsWorkbook.SSubtitle));
            overview.Merges.Add($"A{overview.Rows.Count}:C{overview.Rows.Count}");
        }
        overview.AddBlank();
        overview.Add(
            SzStatisticsWorkbook.Cell.Text("Показатель", SzStatisticsWorkbook.SHeader),
            SzStatisticsWorkbook.Cell.Text("Записок", SzStatisticsWorkbook.SHeader),
            SzStatisticsWorkbook.Cell.Text("Доля", SzStatisticsWorkbook.SHeader));
        overview.RowHeights[overview.Rows.Count] = 22;

        double Share(int part, int whole) => whole == 0 ? 0 : (double)part / whole;

        void Kpi(string label, int value, bool alarm = false) => overview.Add(
            SzStatisticsWorkbook.Cell.Text(label, SzStatisticsWorkbook.SKpiLabel),
            SzStatisticsWorkbook.Cell.Num(value, alarm && value > 0 ? SzStatisticsWorkbook.SIntOverdue : SzStatisticsWorkbook.SInt),
            SzStatisticsWorkbook.Cell.Num(Share(value, summary.Total), SzStatisticsWorkbook.SPercent));

        Kpi("Всего записок", summary.Total);
        Kpi("В работе", summary.InWork);
        Kpi("Просрочено", summary.Overdue, alarm: true);
        Kpi("Исполнено", summary.Executed);
        Kpi("Прочее (черновики, отозванные, забракованные)", summary.Other);
        overview.AddBlank();
        overview.Add(
            SzStatisticsWorkbook.Cell.Text("Подразделений с записками", SzStatisticsWorkbook.SKpiLabel),
            SzStatisticsWorkbook.Cell.Num(summary.ByUnit.Count));
        overview.Add(
            SzStatisticsWorkbook.Cell.Text("Подразделений с просрочкой", SzStatisticsWorkbook.SKpiLabel),
            SzStatisticsWorkbook.Cell.Num(summary.ByUnit.Count(x => x.Value.Overdue > 0),
                summary.ByUnit.Any(x => x.Value.Overdue > 0) ? SzStatisticsWorkbook.SIntOverdue : SzStatisticsWorkbook.SInt));
        overview.Add(
            SzStatisticsWorkbook.Cell.Text("Поручений всего", SzStatisticsWorkbook.SKpiLabel),
            SzStatisticsWorkbook.Cell.Num(rows.Sum(r => r.AssignmentCount)));
        var overdueAssignments = rows.Sum(r => r.OverdueAssignments);
        overview.Add(
            SzStatisticsWorkbook.Cell.Text("Просроченных поручений", SzStatisticsWorkbook.SKpiLabel),
            SzStatisticsWorkbook.Cell.Num(overdueAssignments,
                overdueAssignments > 0 ? SzStatisticsWorkbook.SIntOverdue : SzStatisticsWorkbook.SInt));
        overview.AddBlank();
        overview.Add(SzStatisticsWorkbook.Cell.Text(
            "«Просрочено» - записка со сроком в прошлом или с незакрытым поручением,", SzStatisticsWorkbook.SSubtitle));
        overview.Add(SzStatisticsWorkbook.Cell.Text(
            "срок которого истёк. «Прочее» в работу не считается.", SzStatisticsWorkbook.SSubtitle));

        // ── Разрезы ──
        SzStatisticsWorkbook.Sheet Breakdown(string name, string title, string firstColumn, double firstWidth,
            IEnumerable<KeyValuePair<string, SzStatisticsCell>> cells, Func<string, string> label)
        {
            var sheet = new SzStatisticsWorkbook.Sheet
            {
                Name = name,
                Widths = [firstWidth, 11, 12, 13, 12, 11, 15, 15],
                FreezeRows = 4,
            };
            Caption(sheet, title, 7);
            sheet.Add(
                SzStatisticsWorkbook.Cell.Text(firstColumn, SzStatisticsWorkbook.SHeader),
                SzStatisticsWorkbook.Cell.Text("Всего", SzStatisticsWorkbook.SHeader),
                SzStatisticsWorkbook.Cell.Text("В работе", SzStatisticsWorkbook.SHeader),
                SzStatisticsWorkbook.Cell.Text("Просрочено", SzStatisticsWorkbook.SHeader),
                SzStatisticsWorkbook.Cell.Text("Исполнено", SzStatisticsWorkbook.SHeader),
                SzStatisticsWorkbook.Cell.Text("Прочее", SzStatisticsWorkbook.SHeader),
                SzStatisticsWorkbook.Cell.Text("Доля просрочки", SzStatisticsWorkbook.SHeader),
                SzStatisticsWorkbook.Cell.Text("Доля от всех записок", SzStatisticsWorkbook.SHeader));
            sheet.RowHeights[4] = 30;

            var list = cells.ToList();
            foreach (var (key, cell) in list)
            {
                sheet.Add(
                    SzStatisticsWorkbook.Cell.Text(label(key), SzStatisticsWorkbook.STextWrap),
                    SzStatisticsWorkbook.Cell.Num(cell.Total),
                    SzStatisticsWorkbook.Cell.Num(cell.InWork),
                    SzStatisticsWorkbook.Cell.Num(cell.Overdue,
                        cell.Overdue > 0 ? SzStatisticsWorkbook.SIntOverdue : SzStatisticsWorkbook.SIntMuted),
                    SzStatisticsWorkbook.Cell.Num(cell.Executed),
                    SzStatisticsWorkbook.Cell.Num(cell.Other, SzStatisticsWorkbook.SIntMuted),
                    SzStatisticsWorkbook.Cell.Num(Share(cell.Overdue, cell.Total), SzStatisticsWorkbook.SPercent),
                    SzStatisticsWorkbook.Cell.Num(Share(cell.Total, summary.Total), SzStatisticsWorkbook.SPercent));
            }

            if (list.Count == 0)
            {
                sheet.Add(SzStatisticsWorkbook.Cell.Text("Нет записок за выбранный период", SzStatisticsWorkbook.SSubtitle));
                return sheet;
            }

            sheet.AutoFilter = $"A4:H{sheet.Rows.Count}";
            sheet.Add(
                SzStatisticsWorkbook.Cell.Text("Итого", SzStatisticsWorkbook.STotalLabel),
                SzStatisticsWorkbook.Cell.Num(summary.Total, SzStatisticsWorkbook.STotalInt),
                SzStatisticsWorkbook.Cell.Num(summary.InWork, SzStatisticsWorkbook.STotalInt),
                SzStatisticsWorkbook.Cell.Num(summary.Overdue, SzStatisticsWorkbook.STotalInt),
                SzStatisticsWorkbook.Cell.Num(summary.Executed, SzStatisticsWorkbook.STotalInt),
                SzStatisticsWorkbook.Cell.Num(summary.Other, SzStatisticsWorkbook.STotalInt),
                SzStatisticsWorkbook.Cell.Num(Share(summary.Overdue, summary.Total), SzStatisticsWorkbook.STotalPercent),
                SzStatisticsWorkbook.Cell.Num(summary.Total == 0 ? 0 : 1, SzStatisticsWorkbook.STotalPercent));
            return sheet;
        }

        var byUnit = Breakdown("По подразделениям", "Служебные записки по подразделениям", "Подразделение", 52,
            summary.ByUnit.OrderByDescending(x => x.Value.Total).ThenBy(x => x.Key), k => k);
        var byKind = Breakdown("По видам", "Служебные записки по видам", "Вид записки", 40,
            summary.ByKind.OrderByDescending(x => x.Value.Total).ThenBy(x => x.Key), k => k);
        var byMonth = Breakdown("По месяцам", "Служебные записки по месяцам", "Месяц", 20,
            summary.ByMonth.OrderBy(x => Month(x.Key)), MonthTitle);

        // ── Перечень записок ──
        // Сводка отвечает «сколько», а разбираться в просрочке приходится по конкретным номерам.
        var details = new SzStatisticsWorkbook.Sheet
        {
            Name = "Записки",
            Widths = [6, 16, 12, 13, 48, 24, 36, 26, 26, 34, 22, 13, 14, 11, 11, 13, 13],
            FreezeRows = 4,
        };
        Caption(details, "Перечень служебных записок", 16);
        details.Add(
            new[]
            {
                "№", "Рег. номер", "Создана", "Дата регистрации", "Тема", "Вид", "Подразделение", "Автор",
                "Адресат", "Исполнители", "Статус", "Срок исполнения", "Состояние", "Дней просрочки",
                "Поручений", "Просрочено поручений", "Исполнена",
            }.Select(h => SzStatisticsWorkbook.Cell.Text(h, SzStatisticsWorkbook.SHeader)).ToArray());
        details.RowHeights[4] = 30;

        var number = 0;
        foreach (var r in rows.OrderByDescending(r => r.CreatedAt))
        {
            var state = Classify(r, today);
            var daysOverdue = state == SzProgress.Overdue && r.DueDate is { } due && due < today
                ? today.DayNumber - due.DayNumber
                : 0;

            details.Add(
                SzStatisticsWorkbook.Cell.Num(++number, SzStatisticsWorkbook.SCenter),
                SzStatisticsWorkbook.Cell.Text(r.RegNumber ?? "—"),
                SzStatisticsWorkbook.Cell.Date(ToLocalDate(r.CreatedAt)),
                SzStatisticsWorkbook.Cell.Date(r.RegisteredOn),
                SzStatisticsWorkbook.Cell.Text(r.Title, SzStatisticsWorkbook.STextWrap),
                SzStatisticsWorkbook.Cell.Text(r.KindTitle ?? "—", SzStatisticsWorkbook.STextWrap),
                SzStatisticsWorkbook.Cell.Text(r.UnitTitle ?? "—", SzStatisticsWorkbook.STextWrap),
                SzStatisticsWorkbook.Cell.Text(r.AuthorName ?? "—", SzStatisticsWorkbook.STextWrap),
                SzStatisticsWorkbook.Cell.Text(r.AddresseeName ?? "—", SzStatisticsWorkbook.STextWrap),
                SzStatisticsWorkbook.Cell.Text(
                    r.Assignees.Count == 0 ? "—" : string.Join(", ", r.Assignees.Distinct()),
                    SzStatisticsWorkbook.STextWrap),
                SzStatisticsWorkbook.Cell.Text(SzStatusTitles.Title(r.StatusCode), SzStatisticsWorkbook.STextWrap),
                SzStatisticsWorkbook.Cell.Date(r.DueDate),
                SzStatisticsWorkbook.Cell.Text(ProgressTitle(state), state switch
                {
                    SzProgress.Overdue => SzStatisticsWorkbook.SStateOverdue,
                    SzProgress.Executed => SzStatisticsWorkbook.SStateExecuted,
                    SzProgress.InWork => SzStatisticsWorkbook.SStateInWork,
                    _ => SzStatisticsWorkbook.SStateOther,
                }),
                daysOverdue > 0
                    ? SzStatisticsWorkbook.Cell.Num(daysOverdue, SzStatisticsWorkbook.SIntOverdue)
                    : SzStatisticsWorkbook.Cell.Text("—", SzStatisticsWorkbook.SCenter),
                SzStatisticsWorkbook.Cell.Num(r.AssignmentCount),
                SzStatisticsWorkbook.Cell.Num(r.OverdueAssignments,
                    r.OverdueAssignments > 0 ? SzStatisticsWorkbook.SIntOverdue : SzStatisticsWorkbook.SIntMuted),
                SzStatisticsWorkbook.Cell.Date(r.ExecutedAt is { } ex ? ToLocalDate(ex) : null));
        }

        if (number == 0)
            details.Add(SzStatisticsWorkbook.Cell.Text("Нет записок за выбранный период", SzStatisticsWorkbook.SSubtitle));
        else
            details.AutoFilter = $"A4:Q{details.Rows.Count}";

        return SzStatisticsWorkbook.Build(overview, byUnit, byKind, byMonth, details);
    }

    /// <summary>Имя файла выгрузки: «Аналитика СЗ 01.09.2026–30.09.2026.xlsx».</summary>
    public static string ExportFileName(DateOnly? from, DateOnly? to) =>
        $"Аналитика служебных записок {PeriodText(from, to).Replace(" – ", "–")}.xlsx";

    private static string PeriodText(DateOnly? from, DateOnly? to) => (from, to) switch
    {
        (null, null) => "за всё время",
        ({ } f, null) => $"с {f:dd.MM.yyyy}",
        (null, { } t) => $"по {t:dd.MM.yyyy}",
        ({ } f, { } t) => $"{f:dd.MM.yyyy} – {t:dd.MM.yyyy}",
    };

    private static readonly string[] MonthNames =
    [
        "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
        "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь",
    ];

    /// <summary>«09.2026» → «Сентябрь 2026».</summary>
    private static string MonthTitle(string key)
    {
        var date = Month(key);
        return date == DateOnly.MinValue ? key : $"{MonthNames[date.Month - 1]} {date.Year}";
    }

    private DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _clock.Zone));

    // ── внутреннее ───────────────────────────────────────────────────────────

    private async Task<List<SzRow>> LoadAsync(SzStatisticsFilter filter)
    {
        var today = _clock.Today;

        var query = _db.SzDocuments
            .Include(s => s.Document!).ThenInclude(d => d.Author)
            .Include(s => s.Kind)
            .Include(s => s.AuthorUnit)
            .AsNoTracking()
            .Where(s => s.Document != null);

        if (filter.From is { } from)
            query = query.Where(s => s.Document!.CreatedAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (filter.To is { } to)
            query = query.Where(s => s.Document!.CreatedAt <= to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        if (filter.OrgUnitId is { } unitId)
            query = query.Where(s => s.AuthorUnitId == unitId);

        if (filter.KindId is { } kindId)
            query = query.Where(s => s.KindId == kindId);

        return await query
            .Select(s => new SzRow
            {
                RegNumber = s.Document!.RegNumber,
                Title = s.Document.Title,
                StatusCode = s.Document.StatusCode,
                CreatedAt = s.Document.CreatedAt,
                AuthorName = s.Document.Author!.FullName,
                KindTitle = s.Kind!.TitleRu,
                UnitTitle = s.AuthorUnit!.TitleRu,
                DueDate = s.DueDate,
                RegisteredOn = s.RegisteredOn,
                ExecutedAt = s.ExecutedAt,
                AddresseeName = s.AddresseeUser != null ? s.AddresseeUser.FullName : null,
                Assignees = s.Assignments.Select(a => a.AssigneeUser!.FullName).ToList(),
                AssignmentCount = s.Assignments.Count,
                OverdueAssignments = s.Assignments.Count(a =>
                    a.State == SzAssignmentState.Open && a.DueDate != null && a.DueDate < today),
            })
            .ToListAsync();
    }

    /// <summary>
    /// Состояние записки для сводки. Просрочка проверяется раньше исполнения:
    /// записка со сроком в прошлом и незакрытыми поручениями — просрочена, даже если
    /// формальный статус ещё «на исполнении».
    /// </summary>
    private static SzProgress Classify(SzRow row, DateOnly today)
    {
        if (row.StatusCode is SzStatus.Executed or SzStatus.Archived) return SzProgress.Executed;

        if (row.StatusCode is SzStatus.Rejected or SzStatus.Withdrawn or SzStatus.Draft)
            return SzProgress.Other;

        var overdueByAssignment = row.OverdueAssignments > 0;
        var overdueByDueDate = row.DueDate is { } due && due < today;

        if (overdueByAssignment || overdueByDueDate) return SzProgress.Overdue;

        return SzProgress.InWork;
    }

    private static string ProgressTitle(SzProgress progress) => progress switch
    {
        SzProgress.InWork => "В работе",
        SzProgress.Overdue => "Просрочено",
        SzProgress.Executed => "Исполнено",
        _ => "Прочее",
    };

    private static void Bump(Dictionary<string, SzStatisticsCell> map, string key, SzProgress state)
    {
        if (!map.TryGetValue(key, out var cell))
        {
            cell = new SzStatisticsCell();
            map[key] = cell;
        }

        cell.Total++;

        switch (state)
        {
            case SzProgress.InWork: cell.InWork++; break;
            case SzProgress.Overdue: cell.Overdue++; break;
            case SzProgress.Executed: cell.Executed++; break;
            default: cell.Other++; break;
        }
    }

    /// <summary>Ключ «MM.yyyy» для сортировки по времени, а не по строке.</summary>
    private static DateOnly Month(string key) =>
        DateOnly.TryParseExact("01." + key, "dd.MM.yyyy", out var date) ? date : DateOnly.MinValue;

    private enum SzProgress { InWork, Overdue, Executed, Other }

    private class SzRow
    {
        public string? RegNumber { get; init; }
        public string Title { get; init; } = string.Empty;
        public string StatusCode { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public string? AuthorName { get; init; }
        public string? KindTitle { get; init; }
        public string? UnitTitle { get; init; }
        public DateOnly? DueDate { get; init; }
        public DateOnly? RegisteredOn { get; init; }
        public DateTime? ExecutedAt { get; init; }
        public string? AddresseeName { get; init; }
        public List<string> Assignees { get; init; } = [];
        public int AssignmentCount { get; init; }
        public int OverdueAssignments { get; init; }
    }
}

/// <summary>Человеческие названия статусов записки — нужны и в выгрузке, и в сводке.</summary>
public static class SzStatusTitles
{
    public static string Title(string? code) => code switch
    {
        SzStatus.Draft => "Черновик",
        SzStatus.PendingRegistration => "Ждёт регистрации",
        SzStatus.Registered => "Зарегистрирована",
        SzStatus.OnApproval => "На согласовании",
        SzStatus.OnSigning => "На подписании",
        SzStatus.OnBoardReview => "Вынесена на заседание",
        SzStatus.OnRevision => "На доработке",
        SzStatus.OnAddresseeDecision => "На решении адресата",
        SzStatus.OnExecution => "На исполнении",
        SzStatus.Executed => "Исполнена",
        SzStatus.Rejected => "Забракована",
        SzStatus.Withdrawn => "Отозвана",
        SzStatus.Archived => "В архиве",
        null => "—",
        _ => code,
    };
}
