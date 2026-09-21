using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Export;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Substitutions.DTO;
using delosfera_server.Modules.Substitutions.Models;

namespace delosfera_server.Modules.Substitutions.Services;

public interface ISubstitutionStatisticsService
{
    Task<SubstitutionStatisticsDto> GetAsync(SubstitutionStatisticsFilter filter, CancellationToken ct = default);
    Task<byte[]> ExportAsync(SubstitutionStatisticsFilter filter, CancellationToken ct = default);
}

/// <summary>
/// Статистика по заявкам на замещение (ЗМ-SLA): сколько в работе, просрочено, исполнено —
/// в разрезе причин и месяцев, со средним временем прохождения согласования. «Просрочено»
/// считается по текущему этапу маршрута против норматива (SubstitutionSlaSettings), рабочими
/// днями без праздников (праздничный календарь — COND-1).
/// </summary>
public class SubstitutionStatisticsService : ISubstitutionStatisticsService
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;
    private readonly ISubstitutionService _substitutions;

    public SubstitutionStatisticsService(
        DelosferaDbContext db, IBankClock clock, ISubstitutionService substitutions)
    {
        _db = db;
        _clock = clock;
        _substitutions = substitutions;
    }

    private sealed record Row(
        int Id, string? RegNumber, string Subject, SubstitutionReason Reason,
        SubstitutionStatus Status, DateTime CreatedAt,
        DateTime? ActiveActivatedAt, DateTime? FirstActivatedAt, DateTime? LastApprovedAt);

    public async Task<SubstitutionStatisticsDto> GetAsync(SubstitutionStatisticsFilter filter, CancellationToken ct = default)
    {
        var slaDays = await _substitutions.GetSlaDaysAsync(ct);
        var today = _clock.Today;
        var rows = await LoadAsync(filter, ct);

        var dto = new SubstitutionStatisticsDto
        {
            From = filter.From,
            To = filter.To,
            Total = rows.Count,
            SlaDays = slaDays,
        };

        var durations = new List<double>();

        foreach (var r in rows)
        {
            var overdue = IsOverdue(r, today, slaDays);

            Bump(dto.ByReason, ReasonTitle(r.Reason), overdue);
            Bump(dto.ByMonth, r.CreatedAt.ToString("MM.yyyy"), overdue);

            switch (r.Status)
            {
                case SubstitutionStatus.Draft: dto.Draft++; break;
                case SubstitutionStatus.OnApproval: dto.OnApproval++; if (overdue) dto.Overdue++; break;
                case SubstitutionStatus.OnExecution: dto.OnExecution++; break;
                case SubstitutionStatus.Executed: dto.Executed++; break;
                case SubstitutionStatus.Rejected: dto.Rejected++; break;
                case SubstitutionStatus.Withdrawn: dto.Withdrawn++; break;
            }

            // Время прохождения маршрута — по заявкам, которые его прошли (согласование завершено).
            if (r.Status is SubstitutionStatus.OnExecution or SubstitutionStatus.Executed
                && r.FirstActivatedAt is { } start && r.LastApprovedAt is { } end && end > start)
                durations.Add((end - start).TotalDays);
        }

        dto.AvgApprovalDays = durations.Count > 0 ? Math.Round(durations.Average(), 1) : 0;
        return dto;
    }

    public async Task<byte[]> ExportAsync(SubstitutionStatisticsFilter filter, CancellationToken ct = default)
    {
        var summary = await GetAsync(filter, ct);
        var today = _clock.Today;
        var rows = await LoadAsync(filter, ct);

        var byReason = new XlsxSheet
        {
            Name = "По причинам",
            Header = ["Причина", "Всего", "Просрочено"],
            Widths = [40, 12, 14],
            Rows = summary.ByReason
                .OrderByDescending(x => x.Value.Total)
                .Select(x => new[] { x.Key, x.Value.Total.ToString(), x.Value.Overdue.ToString() })
                .ToList(),
        };

        var byMonth = new XlsxSheet
        {
            Name = "По месяцам",
            Header = ["Месяц", "Всего", "Просрочено"],
            Widths = [14, 12, 14],
            Rows = summary.ByMonth
                .OrderBy(x => Month(x.Key))
                .Select(x => new[] { x.Key, x.Value.Total.ToString(), x.Value.Overdue.ToString() })
                .ToList(),
        };

        var details = new XlsxSheet
        {
            Name = "Заявки",
            Header = ["Номер", "Дата", "Тема", "Причина", "Статус", "Просрочено", "Текущий этап открыт, раб. дн."],
            Widths = [16, 13, 46, 30, 22, 14, 28],
            Rows = rows
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new[]
                {
                    r.RegNumber ?? "—",
                    r.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy"),
                    r.Subject,
                    ReasonTitle(r.Reason),
                    StatusTitle(r.Status),
                    IsOverdue(r, today, summary.SlaDays) ? "да" : "нет",
                    r.Status == SubstitutionStatus.OnApproval && r.ActiveActivatedAt is { } a
                        ? BusinessDaysBetween(BankDate(a), today).ToString()
                        : "—",
                })
                .ToList(),
        };

        return XlsxWorkbook.Build(byReason, byMonth, details);
    }

    private async Task<List<Row>> LoadAsync(SubstitutionStatisticsFilter filter, CancellationToken ct)
    {
        var query = _db.SubstitutionRequests.AsNoTracking().AsQueryable();

        if (filter.From is { } from)
        {
            var fromDt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt >= fromDt);
        }
        if (filter.To is { } to)
        {
            var toDt = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(r => r.CreatedAt <= toDt);
        }

        return await query
            .Select(r => new Row(
                r.Id,
                r.RegNumber,
                r.Subject,
                r.Reason,
                r.Status,
                r.CreatedAt,
                r.Approvals.Where(a => a.State == SubstitutionApprovalState.Active)
                    .Select(a => a.ActivatedAt).FirstOrDefault(),
                r.Approvals.Where(a => a.ActivatedAt != null)
                    .OrderBy(a => a.Order).Select(a => a.ActivatedAt).FirstOrDefault(),
                r.Approvals.Where(a => a.State == SubstitutionApprovalState.Approved && a.DecidedAt != null)
                    .OrderByDescending(a => a.DecidedAt).Select(a => a.DecidedAt).FirstOrDefault()))
            .ToListAsync(ct);
    }

    private bool IsOverdue(Row r, DateOnly today, int slaDays)
    {
        if (r.Status != SubstitutionStatus.OnApproval || r.ActiveActivatedAt is not { } a) return false;
        return BusinessDaysBetween(BankDate(a), today) > slaDays;
    }

    private DateOnly BankDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, _clock.Zone));

    private static void Bump(Dictionary<string, SubstitutionStatBucket> map, string key, bool overdue)
    {
        if (!map.TryGetValue(key, out var b)) { b = new SubstitutionStatBucket(); map[key] = b; }
        b.Total++;
        if (overdue) b.Overdue++;
    }

    private static int BusinessDaysBetween(DateOnly from, DateOnly to)
    {
        if (to <= from) return 0;
        var days = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                days++;
        return days;
    }

    private static DateTime Month(string mmYyyy) =>
        DateTime.TryParseExact(mmYyyy, "MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var d)
            ? d : DateTime.MinValue;

    private static string ReasonTitle(SubstitutionReason reason) => reason switch
    {
        SubstitutionReason.Sick => "Больничный",
        SubstitutionReason.Vacation => "Отпуск",
        SubstitutionReason.Dismissal => "Увольнение",
        SubstitutionReason.Other => "Другое",
        _ => reason.ToString(),
    };

    private static string StatusTitle(SubstitutionStatus status) => status switch
    {
        SubstitutionStatus.Draft => "Черновик",
        SubstitutionStatus.OnApproval => "На согласовании",
        SubstitutionStatus.OnExecution => "На исполнении",
        SubstitutionStatus.Executed => "Исполнено",
        SubstitutionStatus.Rejected => "Отклонено",
        SubstitutionStatus.Withdrawn => "Отозвано",
        _ => status.ToString(),
    };
}
