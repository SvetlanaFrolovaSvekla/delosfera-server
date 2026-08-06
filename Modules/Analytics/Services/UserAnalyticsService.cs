using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Extensions;
using delosfera_server.Data;
using delosfera_server.Modules.Analytics.Common;
using delosfera_server.Modules.Analytics.DTO.Request;
using delosfera_server.Modules.Analytics.DTO.Response;
using delosfera_server.Modules.Analytics.DTO.Response.Users;
using delosfera_server.Modules.Vnd.Models;

namespace delosfera_server.Modules.Analytics.Services;

public class UserAnalyticsService : IUserAnalyticsService
{
    private readonly DelosferaDbContext _db;

    public UserAnalyticsService(DelosferaDbContext db)
    {
        _db = db;
    }

    public async Task<UserOverviewResponse> GetOverviewAsync()
    {
        var now = DateTime.UtcNow;
        var monthAgo = now.AddDays(-30);

        var users = await _db.Users
            .Select(u => new { u.IsActive, u.LastLoginAt, u.CreatedAt, u.OrgUnitId })
            .ToListAsync();

        var total = users.Count;
        var active = users.Count(u => u.IsActive);
        var inactive = total - active;
        var activeLast7 = users.Count(u => u.LastLoginAt != null && u.LastLoginAt >= now.AddDays(-7));
        var activeLast30 = users.Count(u => u.LastLoginAt != null && u.LastLoginAt >= monthAgo);
        var neverLoggedIn = users.Count(u => u.LastLoginAt == null);
        var createdLast30 = users.Count(u => u.CreatedAt >= monthAgo);

        var rolesCount = await _db.Roles.CountAsync();
        var orgUnitsWithUsers = users.Where(u => u.OrgUnitId != null).Select(u => u.OrgUnitId).Distinct().Count();

        return new UserOverviewResponse
        {
            Total = total,
            Active = active,
            Inactive = inactive,
            ActiveLast7Days = activeLast7,
            ActiveLast30Days = activeLast30,
            NeverLoggedIn = neverLoggedIn,
            CreatedLast30Days = createdLast30,
            RolesCount = rolesCount,
            OrgUnitsWithUsersCount = orgUnitsWithUsers
        };
    }

    public async Task<List<ChartTimePoint>> GetRegistrationTrendAsync(AnalyticsPeriodRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.DateFrom ?? DefaultFrom(today, request.Granularity);
        var to = request.DateTo ?? today;
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        var created = await _db.Users
            .Where(u => u.CreatedAt >= fromDt && u.CreatedAt <= toDt)
            .Select(u => u.CreatedAt)
            .ToListAsync();

        var buckets = GeneratePeriods(from, to, request.Granularity);

        return buckets.Select(b => new ChartTimePoint
        {
            PeriodStart = b.Start,
            PeriodLabel = b.Label,
            Value = created.Count(d => BucketStart(DateOnly.FromDateTime(d), request.Granularity) == b.Start)
        }).ToList();
    }

    public async Task<List<ChartCategoryPoint>> GetRoleDistributionAsync(string language)
    {
        var users = await _db.Users.Include(u => u.Roles).ToListAsync();
        var total = users.Count;

        var counts = users
            .SelectMany(u => u.Roles.Select(r => (r.Id, Title: r.ResolveTitle(language))))
            .GroupBy(x => (x.Id, x.Title))
            .Select(g => new ChartCategoryPoint
            {
                Id = g.Key.Id,
                Label = g.Key.Title,
                Value = g.Count(),
                Percent = total > 0 ? Math.Round(g.Count() * 100.0 / total, 1) : 0
            })
            .OrderByDescending(c => c.Value)
            .ToList();

        var withoutRole = users.Count(u => u.Roles.Count == 0);
        if (withoutRole > 0)
        {
            counts.Add(new ChartCategoryPoint
            {
                Id = null,
                Label = language == "en" ? "No role" : language == "kg" ? "Ролсуз" : "Без роли",
                Value = withoutRole,
                Percent = total > 0 ? Math.Round(withoutRole * 100.0 / total, 1) : 0
            });
        }

        return counts;
    }

    public async Task<List<ChartCategoryPoint>> GetOrgUnitDistributionAsync(string language, int top = 10)
    {
        var counts = await _db.Users
            .Where(u => u.OrgUnitId != null)
            .GroupBy(u => u.OrgUnitId!.Value)
            .Select(g => new { OrgUnitId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var withoutOrgUnit = await _db.Users.CountAsync(u => u.OrgUnitId == null);
        var total = counts.Sum(c => c.Count) + withoutOrgUnit;

        var topCounts = counts.Take(top).ToList();
        var otherCount = counts.Skip(top).Sum(c => c.Count);

        var ids = topCounts.Select(c => c.OrgUnitId).ToList();
        var orgUnits = await _db.OrganizationUnits.Where(o => ids.Contains(o.Id)).ToListAsync();

        var result = topCounts.Select(c =>
        {
            var org = orgUnits.FirstOrDefault(o => o.Id == c.OrgUnitId);
            return new ChartCategoryPoint
            {
                Id = c.OrgUnitId,
                Label = org != null ? org.ResolveTitle(language) : $"#{c.OrgUnitId}",
                Value = c.Count,
                Percent = total > 0 ? Math.Round(c.Count * 100.0 / total, 1) : 0
            };
        }).ToList();

        if (otherCount > 0)
        {
            result.Add(new ChartCategoryPoint
            {
                Id = null,
                Label = language == "en" ? "Others" : language == "kg" ? "Башкалар" : "Остальные",
                Value = otherCount,
                Percent = total > 0 ? Math.Round(otherCount * 100.0 / total, 1) : 0
            });
        }

        if (withoutOrgUnit > 0)
        {
            result.Add(new ChartCategoryPoint
            {
                Id = null,
                Label = language == "en" ? "No department" : language == "kg" ? "Бөлүмсүз" : "Без подразделения",
                Value = withoutOrgUnit,
                Percent = total > 0 ? Math.Round(withoutOrgUnit * 100.0 / total, 1) : 0
            });
        }

        return result;
    }

    public async Task<UserActivityBucketsResponse> GetActivityBucketsAsync()
    {
        var now = DateTime.UtcNow;
        var logins = await _db.Users.Select(u => u.LastLoginAt).ToListAsync();

        return new UserActivityBucketsResponse
        {
            Today = logins.Count(l => l != null && l >= now.Date),
            Last7Days = logins.Count(l => l != null && l < now.Date && l >= now.AddDays(-7)),
            Last30Days = logins.Count(l => l != null && l < now.AddDays(-7) && l >= now.AddDays(-30)),
            Last90Days = logins.Count(l => l != null && l < now.AddDays(-30) && l >= now.AddDays(-90)),
            Inactive90PlusDays = logins.Count(l => l != null && l < now.AddDays(-90)),
            NeverLoggedIn = logins.Count(l => l == null)
        };
    }

    public async Task<List<UserTopInitiatorItem>> GetTopInitiatorsAsync(int top = 10)
    {
        var createdCounts = await _db.VndDocuments
            .Where(v => v.CreatedByUserId != null)
            .GroupBy(v => v.CreatedByUserId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Count(),
                Active = g.Count(v => v.Status == VndStatus.Active)
            })
            .OrderByDescending(x => x.Total)
            .Take(top)
            .ToListAsync();

        var userIds = createdCounts.Select(c => c.UserId).ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, OrgUnitTitle = u.OrgUnit != null ? u.OrgUnit.TitleRu : null })
            .ToListAsync();

        var actualizationCounts = await _db.Set<VndActualizationRecord>()
            .Where(r => userIds.Contains(r.ResponsibleUserId))
            .GroupBy(r => r.ResponsibleUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        return createdCounts.Select(c =>
        {
            var user = users.FirstOrDefault(u => u.Id == c.UserId);
            return new UserTopInitiatorItem
            {
                UserId = c.UserId,
                FullName = user?.FullName ?? $"#{c.UserId}",
                OrgUnitLabel = user?.OrgUnitTitle,
                VndCreatedCount = c.Total,
                VndActiveCount = c.Active,
                ActualizationCyclesCount = actualizationCounts.FirstOrDefault(a => a.UserId == c.UserId)?.Count ?? 0
            };
        }).ToList();
    }

    public async Task<List<UserApproverPerformanceItem>> GetApproverPerformanceAsync(int top = 20)
    {
        var stages = await _db.VndApprovalStages
            .Select(s => new
            {
                s.ApproverUserId,
                s.PrimaryDecision,
                s.PrimaryDecidedAt,
                s.CreatedAt,
                s.RepeatDecision,
                s.RepeatDecidedAt,
                s.FinalHoldDecision,
                s.FinalHoldDecidedAt
            })
            .ToListAsync();

        var users = await _db.Users
            .Select(u => new { u.Id, u.FullName, OrgUnitTitle = u.OrgUnit != null ? u.OrgUnit.TitleRu : null })
            .ToDictionaryAsync(u => u.Id, u => u);

        var result = stages
            .GroupBy(s => s.ApproverUserId)
            .Select(group =>
            {
                var decided = new List<(ApprovalStageDecision Decision, double Hours)>();

                foreach (var s in group)
                {
                    if (s.PrimaryDecision != ApprovalStageDecision.Pending)
                        decided.Add((s.PrimaryDecision, (s.PrimaryDecidedAt!.Value - s.CreatedAt).TotalHours));

                    if (s.RepeatDecision.HasValue && s.RepeatDecision != ApprovalStageDecision.Pending)
                        decided.Add((s.RepeatDecision.Value, (s.RepeatDecidedAt!.Value - s.CreatedAt).TotalHours));

                    if (s.FinalHoldDecision.HasValue && s.FinalHoldDecision != ApprovalStageDecision.Pending)
                        decided.Add((s.FinalHoldDecision.Value, (s.FinalHoldDecidedAt!.Value - s.CreatedAt).TotalHours));
                }

                var onTime = decided.Where(d => d.Decision != ApprovalStageDecision.AutoApprovedByTimeout).ToList();
                var timeout = decided.Count(d => d.Decision == ApprovalStageDecision.AutoApprovedByTimeout);
                var user = users.GetValueOrDefault(group.Key);

                return new UserApproverPerformanceItem
                {
                    UserId = group.Key,
                    FullName = user?.FullName ?? $"#{group.Key}",
                    OrgUnitLabel = user?.OrgUnitTitle,
                    TotalDecisions = decided.Count,
                    TimeoutDecisions = timeout,
                    AverageDecisionHours = onTime.Count > 0 ? Math.Round(onTime.Average(d => d.Hours), 1) : 0
                };
            })
            .Where(x => x.TotalDecisions > 0)
            .OrderByDescending(x => x.TotalDecisions)
            .Take(top)
            .ToList();

        return result;
    }

    // --- Вспомогательные методы (аналогичны используемым в VndAnalyticsService) ---

    private static DateOnly DefaultFrom(DateOnly today, AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Day => today.AddDays(-29),
        AnalyticsGranularity.Week => today.AddDays(-7 * 11),
        AnalyticsGranularity.Month => today.AddMonths(-11),
        AnalyticsGranularity.Quarter => today.AddMonths(-3 * 7),
        AnalyticsGranularity.Year => today.AddYears(-4),
        _ => today.AddMonths(-11)
    };

    private static DateOnly BucketStart(DateOnly date, AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Day => date,
        AnalyticsGranularity.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        AnalyticsGranularity.Month => new DateOnly(date.Year, date.Month, 1),
        AnalyticsGranularity.Quarter => new DateOnly(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
        AnalyticsGranularity.Year => new DateOnly(date.Year, 1, 1),
        _ => new DateOnly(date.Year, date.Month, 1)
    };

    private static string BucketLabel(DateOnly bucketStart, AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Day => bucketStart.ToString("dd.MM.yyyy"),
        AnalyticsGranularity.Week => $"{bucketStart:dd.MM} — {bucketStart.AddDays(6):dd.MM.yyyy}",
        AnalyticsGranularity.Month => bucketStart.ToString("MMMM yyyy"),
        AnalyticsGranularity.Quarter => $"Q{((bucketStart.Month - 1) / 3) + 1} {bucketStart.Year}",
        AnalyticsGranularity.Year => bucketStart.Year.ToString(),
        _ => bucketStart.ToString("MMMM yyyy")
    };

    private static List<(DateOnly Start, string Label)> GeneratePeriods(DateOnly from, DateOnly to, AnalyticsGranularity granularity)
    {
        var result = new List<(DateOnly, string)>();
        var cursor = BucketStart(from, granularity);
        var end = BucketStart(to, granularity);
        var guard = 0;

        while (cursor <= end && guard < 500)
        {
            result.Add((cursor, BucketLabel(cursor, granularity)));

            cursor = granularity switch
            {
                AnalyticsGranularity.Day => cursor.AddDays(1),
                AnalyticsGranularity.Week => cursor.AddDays(7),
                AnalyticsGranularity.Month => cursor.AddMonths(1),
                AnalyticsGranularity.Quarter => cursor.AddMonths(3),
                AnalyticsGranularity.Year => cursor.AddYears(1),
                _ => cursor.AddMonths(1)
            };
            guard++;
        }

        return result;
    }
}
