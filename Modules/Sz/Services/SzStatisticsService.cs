using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Export;
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

    public async Task<SzStatisticsDto> GetAsync(SzStatisticsFilter filter)
    {
        var today = _clock.Today;
        var rows = await LoadAsync(filter);

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
        var summary = await GetAsync(filter);
        var today = _clock.Today;
        var rows = await LoadAsync(filter);

        var byUnit = new XlsxSheet
        {
            Name = "По подразделениям",
            Header = ["Подразделение", "Всего", "В работе", "Просрочено", "Исполнено", "Прочее"],
            Widths = [46, 10, 12, 14, 12, 12],
            Rows = summary.ByUnit
                .OrderByDescending(x => x.Value.Total)
                .Select(x => Line(x.Key, x.Value))
                .ToList(),
        };

        var byKind = new XlsxSheet
        {
            Name = "По видам",
            Header = ["Вид записки", "Всего", "В работе", "Просрочено", "Исполнено", "Прочее"],
            Widths = [46, 10, 12, 14, 12, 12],
            Rows = summary.ByKind
                .OrderByDescending(x => x.Value.Total)
                .Select(x => Line(x.Key, x.Value))
                .ToList(),
        };

        var byMonth = new XlsxSheet
        {
            Name = "По месяцам",
            Header = ["Месяц", "Всего", "В работе", "Просрочено", "Исполнено", "Прочее"],
            Widths = [14, 10, 12, 14, 12, 12],
            Rows = summary.ByMonth
                .OrderBy(x => Month(x.Key))
                .Select(x => Line(x.Key, x.Value))
                .ToList(),
        };

        // Лист с самими записками: сводка отвечает «сколько», а разбираться в
        // просрочке приходится по конкретным номерам.
        var details = new XlsxSheet
        {
            Name = "Записки",
            Header =
            [
                "Номер", "Дата", "Тема", "Вид", "Подразделение", "Автор",
                "Статус", "Срок", "Состояние", "Поручений", "Просрочено поручений",
            ],
            Widths = [18, 13, 50, 24, 40, 26, 22, 13, 18, 12, 20],
            Rows = rows
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new[]
                {
                    r.RegNumber ?? "—",
                    r.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy"),
                    r.Title,
                    r.KindTitle ?? "—",
                    r.UnitTitle ?? "—",
                    r.AuthorName ?? "—",
                    SzStatusTitles.Title(r.StatusCode),
                    r.DueDate?.ToString("dd.MM.yyyy") ?? "—",
                    ProgressTitle(Classify(r, today)),
                    r.AssignmentCount.ToString(),
                    r.OverdueAssignments.ToString(),
                })
                .ToList(),
        };

        return XlsxWorkbook.Build(byUnit, byKind, byMonth, details);
    }

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

    private static string[] Line(string key, SzStatisticsCell cell) =>
    [
        key,
        cell.Total.ToString(),
        cell.InWork.ToString(),
        cell.Overdue.ToString(),
        cell.Executed.ToString(),
        cell.Other.ToString(),
    ];

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
