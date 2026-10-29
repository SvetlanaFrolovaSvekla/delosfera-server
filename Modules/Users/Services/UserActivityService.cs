using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Users.DTO.Request;
using delosfera_server.Modules.Users.DTO.Response;
using Microsoft.EntityFrameworkCore;

namespace delosfera_server.Modules.Users.Services;

/// <summary>Виды действий в ленте активности пользователя (цвет точки и фильтр на клиенте).</summary>
public static class UserActivityKinds
{
    public const string Created = "created";
    public const string ProcessStarted = "processStarted";
    public const string Approved = "approved";
    public const string ApprovedWithComment = "approvedWithComment";
    public const string Rejected = "rejected";
    public const string AutoApproved = "autoApproved";
    public const string Removed = "removed";
    public const string Other = "other";

    public static string FromDecision(ApprovalStageDecision d) => d switch
    {
        ApprovalStageDecision.Approved => Approved,
        ApprovalStageDecision.ApprovedWithComment => ApprovedWithComment,
        ApprovalStageDecision.Rejected => Rejected,
        ApprovalStageDecision.AutoApprovedByTimeout => AutoApproved,
        ApprovalStageDecision.RemovedByEditor => Removed,
        _ => Other
    };
}

/// <summary>
/// Лента активности пользователя. Отдельной таблицы аудита пока нет, поэтому активность
/// выводится из существующих данных ВНД: созданные документы, принятые решения по
/// согласованию и инициированные согласования.
///
/// Пагинация — курсором (keyset), а не Skip: три источника сливаются в одну ленту,
/// упорядоченную по (время ↓, источник ↓, id ↓). Из каждого источника берётся не больше
/// pageSize + 1 записей после курсора, так что стоимость страницы не растёт с её номером.
/// </summary>
public class UserActivityService : IUserActivityService
{
    // Источники ленты; номер участвует в порядке и в курсоре (разводит записи с одинаковым временем).
    private const int SourceCreated = 0;
    private const int SourceInitiated = 1;
    private const int SourceDecided = 2;

    private const string CreatedText = "Создан документ";
    private const string InitiatedText = "Инициировано согласование";

    private static readonly ApprovalStageDecision[] AllDecisions = Enum.GetValues<ApprovalStageDecision>();

    private readonly DelosferaDbContext _db;

    public UserActivityService(DelosferaDbContext db) => _db = db;

    /// <summary>Общая форма записи из любого источника — по ней фильтруем в базе.</summary>
    private sealed class ActivityRow
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int? VndId { get; set; }
        public string? Code { get; set; }
        public string? TitleRu { get; set; }
        public string? TitleEn { get; set; }
        public string? TitleKg { get; set; }
        public ApprovalStageDecision? Decision { get; set; }
    }

    private readonly record struct Cursor(DateTime Timestamp, int Source, int Id);

    public async Task<UserActivityResponse> GetActivityAsync(int userId, string languageCode, UserActivityQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        // Границы периода в UTC (как в журнале аудита): в базе время хранится в UTC.
        var fromUtc = query.From?.ToUniversalTime();
        var toUtc = query.To?.ToUniversalTime();
        var terms = SplitTerms(query.Search);
        var kinds = (query.Kinds ?? [])
            .SelectMany(k => k.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cursor = ParseCursor(query.Cursor);

        // --- Источники с периодом и поиском (без видов и курсора) ---
        var created = Filter(CreatedRows(userId), fromUtc, toUtc, terms, CreatedText, decided: false);
        var initiated = Filter(InitiatedRows(userId), fromUtc, toUtc, terms, InitiatedText, decided: false);
        var decided = Filter(DecidedRows(userId), fromUtc, toUtc, terms, null, decided: true);

        // --- Счётчики по видам (для легенды) — только на первой странице ---
        Dictionary<string, int>? kindCounts = null;
        if (cursor is null)
        {
            kindCounts = new Dictionary<string, int>
            {
                [UserActivityKinds.Created] = await created.CountAsync(),
                [UserActivityKinds.ProcessStarted] = await initiated.CountAsync(),
            };
            var byDecision = await decided
                .GroupBy(r => r.Decision)
                .Select(g => new { Decision = g.Key, Count = g.Count() })
                .ToListAsync();
            foreach (var g in byDecision)
            {
                var kind = UserActivityKinds.FromDecision(g.Decision ?? ApprovalStageDecision.Pending);
                kindCounts[kind] = kindCounts.GetValueOrDefault(kind) + g.Count;
            }
        }

        // --- Отбор по видам ---
        var takeCreated = kinds.Count == 0 || kinds.Contains(UserActivityKinds.Created);
        var takeInitiated = kinds.Count == 0 || kinds.Contains(UserActivityKinds.ProcessStarted);
        var decisions = AllDecisions.Where(d => kinds.Count == 0 || kinds.Contains(UserActivityKinds.FromDecision(d))).ToList();
        if (kinds.Count > 0)
        {
            var decisionValues = decisions.Cast<ApprovalStageDecision?>().ToList();
            decided = decided.Where(r => decisionValues.Contains(r.Decision));
        }

        // --- Страница: из каждого источника pageSize + 1 записей после курсора ---
        var rows = new List<(int Source, ActivityRow Row)>();
        if (takeCreated) rows.AddRange((await Page(created, SourceCreated, cursor, pageSize)).Select(r => (SourceCreated, r)));
        if (takeInitiated) rows.AddRange((await Page(initiated, SourceInitiated, cursor, pageSize)).Select(r => (SourceInitiated, r)));
        if (decisions.Count > 0) rows.AddRange((await Page(decided, SourceDecided, cursor, pageSize)).Select(r => (SourceDecided, r)));

        var merged = rows
            .OrderByDescending(x => x.Row.Timestamp)
            .ThenByDescending(x => x.Source)
            .ThenByDescending(x => x.Row.Id)
            .ToList();
        var page = merged.Take(pageSize).ToList();
        var hasMore = merged.Count > pageSize;
        var last = page.LastOrDefault();

        return new UserActivityResponse
        {
            // Общие счётчики — как раньше, без учёта фильтров.
            VndCreatedCount = await _db.VndDocuments.CountAsync(v => v.CreatedByUserId == userId),
            ApprovalsInitiatedCount = await _db.VndApprovalProcesses.CountAsync(p => p.InitiatorUserId == userId),
            ApprovalsDecidedCount = await _db.VndApprovalStages.CountAsync(s =>
                s.ApproverUserId == userId &&
                (s.PrimaryDecidedAt != null || s.RepeatDecidedAt != null || s.FinalHoldDecidedAt != null)),
            Recent = page.Select(x => ToItem(x.Source, x.Row, languageCode)).ToList(),
            HasMore = hasMore,
            NextCursor = hasMore && last.Row is not null ? FormatCursor(last.Row.Timestamp, last.Source, last.Row.Id) : null,
            KindCounts = kindCounts,
        };
    }

    // ---------- Источники ----------

    private IQueryable<ActivityRow> CreatedRows(int userId) => _db.VndDocuments.AsNoTracking()
        .Where(v => v.CreatedByUserId == userId)
        .Select(v => new ActivityRow
        {
            Id = v.Id, Timestamp = v.CreatedAt, VndId = v.Id, Code = v.Code,
            TitleRu = v.TitleRu, TitleEn = v.TitleEn, TitleKg = v.TitleKg, Decision = null
        });

    private IQueryable<ActivityRow> InitiatedRows(int userId) => _db.VndApprovalProcesses.AsNoTracking()
        .Where(p => p.InitiatorUserId == userId)
        .Select(p => new ActivityRow
        {
            Id = p.Id, Timestamp = p.PrimaryStartedAt, VndId = p.VndId, Code = p.Vnd!.Code,
            TitleRu = p.Vnd.TitleRu, TitleEn = p.Vnd.TitleEn, TitleKg = p.Vnd.TitleKg, Decision = null
        });

    /// <summary>Решения по согласованию. Этапы идут по порядку (первичное → повторное →
    /// финальное удержание), поэтому последнее решение — первое непустое с конца.</summary>
    private IQueryable<ActivityRow> DecidedRows(int userId) => _db.VndApprovalStages.AsNoTracking()
        .Where(s => s.ApproverUserId == userId &&
                    (s.FinalHoldDecidedAt ?? s.RepeatDecidedAt ?? s.PrimaryDecidedAt) != null)
        .Select(s => new ActivityRow
        {
            Id = s.Id,
            Timestamp = (s.FinalHoldDecidedAt ?? s.RepeatDecidedAt ?? s.PrimaryDecidedAt)!.Value,
            VndId = s.ApprovalProcess!.VndId,
            Code = s.ApprovalProcess.Vnd!.Code,
            TitleRu = s.ApprovalProcess.Vnd.TitleRu,
            TitleEn = s.ApprovalProcess.Vnd.TitleEn,
            TitleKg = s.ApprovalProcess.Vnd.TitleKg,
            Decision = s.FinalHoldDecision ?? s.RepeatDecision ?? s.PrimaryDecision
        });

    // ---------- Фильтры ----------

    /// <param name="fixedText">Описание, одинаковое для всех записей источника (для поиска по нему).</param>
    /// <param name="decided">Источник решений: описание зависит от решения.</param>
    private static IQueryable<ActivityRow> Filter(
        IQueryable<ActivityRow> q, DateTime? fromUtc, DateTime? toUtc, List<string> terms, string? fixedText, bool decided)
    {
        if (fromUtc is { } f) q = q.Where(r => r.Timestamp >= f);
        if (toUtc is { } t) q = q.Where(r => r.Timestamp <= t);

        foreach (var term in terms)
        {
            // Слово уже есть в описании, общем для всего источника, — по нему отбирать нечего.
            if (fixedText is not null && fixedText.Contains(term, StringComparison.OrdinalIgnoreCase)) continue;

            var pattern = $"%{EscapeLike(term)}%";
            if (decided)
            {
                var matching = AllDecisions
                    .Where(d => DescribeDecision(d).Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Cast<ApprovalStageDecision?>()
                    .ToList();
                q = q.Where(r =>
                    matching.Contains(r.Decision) ||
                    (r.Code != null && EF.Functions.ILike(r.Code, pattern, "\\")) ||
                    (r.TitleRu != null && EF.Functions.ILike(r.TitleRu, pattern, "\\")) ||
                    (r.TitleEn != null && EF.Functions.ILike(r.TitleEn, pattern, "\\")) ||
                    (r.TitleKg != null && EF.Functions.ILike(r.TitleKg, pattern, "\\")));
            }
            else
            {
                q = q.Where(r =>
                    (r.Code != null && EF.Functions.ILike(r.Code, pattern, "\\")) ||
                    (r.TitleRu != null && EF.Functions.ILike(r.TitleRu, pattern, "\\")) ||
                    (r.TitleEn != null && EF.Functions.ILike(r.TitleEn, pattern, "\\")) ||
                    (r.TitleKg != null && EF.Functions.ILike(r.TitleKg, pattern, "\\")));
            }
        }

        return q;
    }

    /// <summary>Записи источника после курсора в порядке (время ↓, источник ↓, id ↓).</summary>
    private static Task<List<ActivityRow>> Page(IQueryable<ActivityRow> q, int source, Cursor? cursor, int pageSize)
    {
        if (cursor is { } c)
        {
            var ts = c.Timestamp;
            var id = c.Id;
            if (source < c.Source) q = q.Where(r => r.Timestamp <= ts);
            else if (source == c.Source) q = q.Where(r => r.Timestamp < ts || (r.Timestamp == ts && r.Id < id));
            else q = q.Where(r => r.Timestamp < ts);
        }

        return q.OrderByDescending(r => r.Timestamp).ThenByDescending(r => r.Id)
            .Take(pageSize + 1)
            .ToListAsync();
    }

    // ---------- Вспомогательное ----------

    private static UserActivityItemResponse ToItem(int source, ActivityRow r, string lang)
    {
        var title = r.TitleRu is null ? null : Resolve(r.TitleRu, r.TitleEn, r.TitleKg, lang);
        return source switch
        {
            SourceCreated => new UserActivityItemResponse
            {
                Type = "vnd_created", Kind = UserActivityKinds.Created, VndId = r.VndId, VndCode = r.Code,
                VndTitle = title, Timestamp = r.Timestamp, Description = CreatedText
            },
            SourceInitiated => new UserActivityItemResponse
            {
                Type = "approval_initiated", Kind = UserActivityKinds.ProcessStarted, VndId = r.VndId,
                VndCode = r.Code, VndTitle = title, Timestamp = r.Timestamp, Description = InitiatedText
            },
            _ => new UserActivityItemResponse
            {
                Type = "approval_decided",
                Kind = UserActivityKinds.FromDecision(r.Decision ?? ApprovalStageDecision.Pending),
                VndId = r.VndId, VndCode = r.Code, VndTitle = title, Timestamp = r.Timestamp,
                Description = DescribeDecision(r.Decision ?? ApprovalStageDecision.Pending)
            }
        };
    }

    private static List<string> SplitTerms(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? []
            : search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant()).Distinct().Take(10).ToList();

    private static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string FormatCursor(DateTime ts, int source, int id) =>
        $"{ts.ToUniversalTime().Ticks}.{source}.{id}";

    private static Cursor? ParseCursor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split('.');
        if (parts.Length != 3 ||
            !long.TryParse(parts[0], out var ticks) ||
            !int.TryParse(parts[1], out var source) ||
            !int.TryParse(parts[2], out var id) ||
            ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            return null; // испорченный курсор — отдаём первую страницу
        return new Cursor(new DateTime(ticks, DateTimeKind.Utc), source, id);
    }

    private static string Resolve(string ru, string? en, string? kg, string lang) => lang switch
    {
        "en" => string.IsNullOrWhiteSpace(en) ? ru : en!,
        "kg" => string.IsNullOrWhiteSpace(kg) ? ru : kg!,
        _ => ru
    };

    private static string DescribeDecision(ApprovalStageDecision decision) => decision switch
    {
        ApprovalStageDecision.Approved => "Согласовано",
        ApprovalStageDecision.ApprovedWithComment => "Согласовано с замечаниями",
        ApprovalStageDecision.Rejected => "Отклонено",
        ApprovalStageDecision.AutoApprovedByTimeout => "Согласовано автоматически (просрочка)",
        ApprovalStageDecision.RemovedByEditor => "Снят с согласования редактором",
        _ => "Решение по согласованию"
    };
}
