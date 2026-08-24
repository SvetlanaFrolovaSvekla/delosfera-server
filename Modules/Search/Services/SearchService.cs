using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Modules.Search.DTO;

namespace delosfera_server.Modules.Search.Services;

public interface ISearchService
{
    Task<SearchResultDto> SearchAsync(SearchRequest request, int currentUserId);
}

/// <summary>
/// Поиск по документам (GEN-02, GEN-04).
///
/// Ищет по реквизитам карточек и их текстовым полям. Содержимое вложений пока не
/// индексируется: извлечение текста из файлов — отдельная работа, и притворяться,
/// что поиск уже «по документам целиком», хуже, чем честно искать по карточкам.
///
/// Результаты разных контуров сводятся в один список и сортируются по дате: единая
/// релевантность между служебной запиской и протоколом заседания всё равно
/// неубедительна, а «сначала свежее» — понятное и предсказуемое правило.
/// </summary>
public class SearchService : ISearchService
{
    private const int MaxPageSize = 100;
    private const int SnippetRadius = 70;

    private static readonly Regex TermSplitter = new(@"[^\p{L}\p{Nd}]+", RegexOptions.Compiled);

    private readonly DelosferaDbContext _db;
    private readonly IMeetingAccessService _meetingAccess;
    private readonly Common.Services.Authorization.ICurrentUserService _currentUser;

    public SearchService(
        DelosferaDbContext db,
        IMeetingAccessService meetingAccess,
        Common.Services.Authorization.ICurrentUserService currentUser)
    {
        _db = db;
        _meetingAccess = meetingAccess;
        _currentUser = currentUser;
    }

    public async Task<SearchResultDto> SearchAsync(SearchRequest request, int currentUserId)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var page = Math.Max(request.Page, 1);

        var tsQuery = BuildTsQuery(request.Query);
        var scopes = request.Scopes.Count > 0
            ? request.Scopes.Distinct().ToList()
            : Enum.GetValues<SearchScope>().ToList();

        var hits = new List<SearchHitDto>();
        var counts = new Dictionary<string, int>();

        // Берём с запасом на все страницы до текущей: контуры дают разное количество,
        // и обрезать каждый по pageSize значило бы терять записи при слиянии.
        var take = page * pageSize;

        foreach (var scope in scopes)
        {
            var (found, total) = scope switch
            {
                SearchScope.Sz => await SearchSzAsync(request, tsQuery, take),
                SearchScope.Procurement => await SearchProcurementAsync(request, tsQuery, take),
                SearchScope.Contract => await SearchContractsAsync(request, tsQuery, take),
                SearchScope.Meeting => await SearchMeetingsAsync(request, tsQuery, take),
                SearchScope.Vnd => await SearchVndAsync(request, tsQuery, take),
                SearchScope.Correspondence => await SearchCorrespondenceAsync(request, tsQuery, take),
                SearchScope.PowerOfAttorney => await SearchPoaAsync(request, tsQuery, take),
                _ => ([], 0),
            };

            if (total > 0) counts[SearchScopeMap.Title(scope)] = total;
            hits.AddRange(found);
        }

        var ordered = hits.OrderByDescending(h => h.CreatedAt).ToList();

        return new SearchResultDto
        {
            Total = counts.Values.Sum(),
            Page = page,
            PageSize = pageSize,
            CountByScope = counts,
            Items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        };
    }

    // ── контуры ──────────────────────────────────────────────────────────────

    private async Task<(List<SearchHitDto>, int)> SearchSzAsync(
        SearchRequest request, string? tsQuery, int take)
    {
        var query = _db.SzDocuments
            .Include(s => s.Document!).ThenInclude(d => d.Author)
            .Include(s => s.AuthorUnit)
            .AsNoTracking()
            .Where(s => s.Document != null);

        if (request.Statuses.Count > 0)
            query = query.Where(s => request.Statuses.Contains(s.Document!.StatusCode));

        if (request.AuthorId is { } authorId)
            query = query.Where(s => s.Document!.AuthorId == authorId);

        if (request.From is { } from)
            query = query.Where(s => s.Document!.CreatedAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (request.To is { } to)
            query = query.Where(s => s.Document!.CreatedAt <= to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        if (request.OrgUnitId is { } unitId)
            query = query.Where(s => s.AuthorUnitId == unitId);

        if (tsQuery is not null)
        {
            query = query.Where(s =>
                s.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)) ||
                s.Document!.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(s => s.Document!.CreatedAt)
            .Take(take)
            .Select(s => new
            {
                s.Id, s.Body,
                s.Document!.RegNumber, s.Document.Title, s.Document.StatusCode, s.Document.CreatedAt,
                Author = s.Document.Author!.FullName,
                Unit = s.AuthorUnit!.TitleRu,
            })
            .ToListAsync();

        var hits = rows.Select(r => new SearchHitDto
        {
            Scope = SearchScope.Sz,
            ScopeTitle = SearchScopeMap.Title(SearchScope.Sz),
            Id = r.Id,
            RegNumber = r.RegNumber,
            Title = r.Title,
            Snippet = Snippet(r.Body, request.Query),
            StatusTitle = StatusTitle(r.StatusCode),
            AuthorName = r.Author,
            OrgUnitTitle = r.Unit,
            CreatedAt = r.CreatedAt,
            Url = $"/sz/{r.Id}",
        }).ToList();

        return (hits, total);
    }

    private async Task<(List<SearchHitDto>, int)> SearchProcurementAsync(
        SearchRequest request, string? tsQuery, int take)
    {
        var query = _db.ProcurementRequests
            .Include(r => r.Document!).ThenInclude(d => d.Author)
            .Include(r => r.InitiatorUnit)
            .AsNoTracking()
            .Where(r => r.Document != null);

        if (request.Statuses.Count > 0)
            query = query.Where(r => request.Statuses.Contains(r.Document!.StatusCode));

        if (request.AuthorId is { } authorId)
            query = query.Where(r => r.Document!.AuthorId == authorId);

        if (request.From is { } from)
            query = query.Where(r => r.Document!.CreatedAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (request.To is { } to)
            query = query.Where(r => r.Document!.CreatedAt <= to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        if (request.OrgUnitId is { } unitId)
            query = query.Where(r => r.InitiatorUnitId == unitId);

        if (request.AmountFrom is { } amountFrom) query = query.Where(r => r.Amount >= amountFrom);
        if (request.AmountTo is { } amountTo) query = query.Where(r => r.Amount <= amountTo);

        if (tsQuery is not null)
        {
            query = query.Where(r =>
                r.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)) ||
                r.Document!.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(r => r.Document!.CreatedAt)
            .Take(take)
            .Select(r => new
            {
                r.Id, r.Subject, r.Justification, r.Amount,
                r.Document!.RegNumber, r.Document.Title, r.Document.StatusCode, r.Document.CreatedAt,
                Author = r.Document.Author!.FullName,
                Unit = r.InitiatorUnit!.TitleRu,
            })
            .ToListAsync();

        var hits = rows.Select(r => new SearchHitDto
        {
            Scope = SearchScope.Procurement,
            ScopeTitle = SearchScopeMap.Title(SearchScope.Procurement),
            Id = r.Id,
            RegNumber = r.RegNumber,
            Title = string.IsNullOrWhiteSpace(r.Subject) ? r.Title : r.Subject,
            Snippet = Snippet(r.Justification, request.Query),
            StatusTitle = StatusTitle(r.StatusCode),
            AuthorName = r.Author,
            OrgUnitTitle = r.Unit,
            Amount = r.Amount,
            CreatedAt = r.CreatedAt,
            Url = $"/prc/{r.Id}",
        }).ToList();

        return (hits, total);
    }

    private async Task<(List<SearchHitDto>, int)> SearchContractsAsync(
        SearchRequest request, string? tsQuery, int take)
    {
        var query = _db.ProcurementContracts
            .Include(c => c.Document!).ThenInclude(d => d.Author)
            .Include(c => c.Supplier)
            .AsNoTracking()
            .Where(c => c.Document != null);

        if (request.Statuses.Count > 0)
            query = query.Where(c => request.Statuses.Contains(c.Document!.StatusCode));

        if (request.AuthorId is { } authorId)
            query = query.Where(c => c.Document!.AuthorId == authorId);

        if (request.From is { } dateFrom)
            query = query.Where(c => c.Document!.CreatedAt >= dateFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (request.To is { } dateTo)
            query = query.Where(c => c.Document!.CreatedAt <= dateTo.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        if (request.AmountFrom is { } amountFrom) query = query.Where(c => c.Amount >= amountFrom);
        if (request.AmountTo is { } amountTo) query = query.Where(c => c.Amount <= amountTo);

        // У договора своего текстового поля для индексации нет — ищем по карточке
        // документа: там наименование и номер.
        if (tsQuery is not null)
            query = query.Where(c => c.Document!.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)));

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(c => c.Document!.CreatedAt)
            .Take(take)
            .Select(c => new
            {
                c.Id, c.RequestId, c.Amount,
                Supplier = c.Supplier!.Title,
                c.Document!.RegNumber, c.Document.Title, c.Document.StatusCode, c.Document.CreatedAt,
                Author = c.Document.Author!.FullName,
            })
            .ToListAsync();

        var hits = rows.Select(c => new SearchHitDto
        {
            Scope = SearchScope.Contract,
            ScopeTitle = SearchScopeMap.Title(SearchScope.Contract),
            Id = c.Id,
            RegNumber = c.RegNumber,
            Title = c.Title,
            // Предмет договора живёт в наименовании карточки — в подсказке показываем поставщика.
            Snippet = c.Supplier,
            StatusTitle = StatusTitle(c.StatusCode),
            AuthorName = c.Author,
            Amount = c.Amount,
            CreatedAt = c.CreatedAt,
            // Договор живёт панелью в карточке своей закупки.
            Url = $"/prc/{c.RequestId}",
        }).ToList();

        return (hits, total);
    }

    private async Task<(List<SearchHitDto>, int)> SearchMeetingsAsync(
        SearchRequest request, string? tsQuery, int take)
    {
        var query = _db.AgendaItems
            .Include(i => i.Meeting!).ThenInclude(m => m.Secretary)
            .AsNoTracking()
            .Where(i => i.Meeting != null);

        if (request.From is { } from)
            query = query.Where(i => i.Meeting!.Date >= from);

        if (request.To is { } to)
            query = query.Where(i => i.Meeting!.Date <= to);

        if (tsQuery is not null)
            query = query.Where(i => i.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)));

        var rows = await query
            .OrderByDescending(i => i.Meeting!.Date)
            .Take(take * 3)
            .Select(i => new
            {
                i.Id, i.MeetingId, i.Topic, i.Decision, i.ProtocolNumber,
                i.Meeting!.Body, i.Meeting.Number, i.Meeting.Date, i.Meeting.Year,
                Secretary = i.Meeting.Secretary!.FullName,
            })
            .ToListAsync();

        // Вопрос повестки виден не всем: доступ считается по каждому вопросу отдельно,
        // поэтому выдачу фильтруем тем же правилом, что и карточку заседания. Иначе
        // поиск обходил бы ограничение, ради которого это правило и заведено.
        var visibleByMeeting = new Dictionary<int, HashSet<int>>();
        var hits = new List<SearchHitDto>();

        foreach (var row in rows)
        {
            if (!visibleByMeeting.TryGetValue(row.MeetingId, out var visible))
            {
                visible = await _meetingAccess.VisibleItemIdsAsync(row.MeetingId);
                visibleByMeeting[row.MeetingId] = visible;
            }

            if (!visible.Contains(row.Id)) continue;

            hits.Add(new SearchHitDto
            {
                Scope = SearchScope.Meeting,
                ScopeTitle = SearchScopeMap.Title(SearchScope.Meeting),
                Id = row.MeetingId,
                RegNumber = row.ProtocolNumber,
                Title = row.Topic,
                Snippet = Snippet(row.Decision, request.Query),
                StatusTitle = $"{MeetingTitles.Body(row.Body)} № {row.Number:D2} от {row.Date:dd.MM.yyyy}",
                AuthorName = row.Secretary,
                CreatedAt = row.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Url = $"/meetings/{row.MeetingId}",
            });
        }

        return (hits.Take(take).ToList(), hits.Count);
    }

    /// <summary>
    /// Нормативные документы. До сих пор общий поиск их не охватывал — при том что
    /// это самое объёмное содержимое системы и то, что ищут чаще всего.
    /// </summary>
    private async Task<(List<SearchHitDto>, int)> SearchVndAsync(
        SearchRequest request, string? tsQuery, int take)
    {
        var query = _db.VndDocuments
            .Include(v => v.Developer)
            .AsNoTracking()
            .AsQueryable();

        if (request.OrgUnitId is { } unitId)
            query = query.Where(v => v.DeveloperId == unitId);

        if (request.From is { } from)
            query = query.Where(v => v.CreatedAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        if (request.To is { } to)
            query = query.Where(v => v.CreatedAt <= to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

        if (tsQuery is not null)
            query = query.Where(v => v.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)));

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(v => v.CreatedAt)
            .Take(take)
            .Select(v => new
            {
                v.Id, v.Code, v.TitleRu, v.Status, v.CreatedAt,
                Unit = v.Developer!.TitleRu,
            })
            .ToListAsync();

        var hits = rows.Select(r => new SearchHitDto
        {
            Scope = SearchScope.Vnd,
            ScopeTitle = SearchScopeMap.Title(SearchScope.Vnd),
            Id = r.Id,
            RegNumber = r.Code,
            Title = r.TitleRu,
            StatusTitle = VndStatusTitle(r.Status),
            OrgUnitTitle = r.Unit,
            CreatedAt = r.CreatedAt,
            Url = $"/base-vnd/{r.Id}",
        }).ToList();

        return (hits, total);
    }

    /// <summary>
    /// Корреспонденция. Ограничение по банковской тайне применяется здесь же:
    /// общий поиск не должен показывать существование запроса по счетам тому,
    /// кому запрос не положен.
    /// </summary>
    private async Task<(List<SearchHitDto>, int)> SearchCorrespondenceAsync(
        SearchRequest request, string? tsQuery, int take)
    {
        var query = _db.CorrespondenceLetters
            .Include(l => l.Correspondent)
            .Include(l => l.ResponsibleUnit)
            .AsNoTracking()
            .AsQueryable();

        if (!_currentUser.HasPermission(Users.Models.PermissionCode.ViewBankSecrecyInquiries))
        {
            var userId = _currentUser.UserId;

            query = query.Where(l =>
                l.Category != Correspondence.Models.LetterCategory.BankSecrecyInquiry
                || l.ResponsibleUserId == userId
                || l.ResolutionByUserId == userId
                || l.CreatedByUserId == userId);
        }

        if (request.OrgUnitId is { } unitId)
            query = query.Where(l => l.ResponsibleUnitId == unitId);

        if (request.From is { } from)
            query = query.Where(l => l.RegisteredOn >= from);

        if (request.To is { } to)
            query = query.Where(l => l.RegisteredOn <= to);

        if (tsQuery is not null)
            query = query.Where(l => l.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)));

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(l => l.RegisteredOn).ThenByDescending(l => l.Id)
            .Take(take)
            .Select(l => new
            {
                l.Id, l.RegNumber, l.Subject, l.Summary, l.Direction, l.Status, l.CreatedAt,
                Correspondent = l.Correspondent!.Title,
                Unit = l.ResponsibleUnit!.TitleRu,
            })
            .ToListAsync();

        var hits = rows.Select(r => new SearchHitDto
        {
            Scope = SearchScope.Correspondence,
            ScopeTitle = SearchScopeMap.Title(SearchScope.Correspondence),
            Id = r.Id,
            RegNumber = r.RegNumber,
            Title = r.Subject,
            Snippet = Snippet(r.Summary, request.Query),
            StatusTitle = r.Direction == Correspondence.Models.LetterDirection.Incoming
                ? $"Входящее · {r.Correspondent}"
                : $"Исходящее · {r.Correspondent}",
            OrgUnitTitle = r.Unit,
            CreatedAt = r.CreatedAt,
            Url = $"/correspondence/{r.Id}",
        }).ToList();

        return (hits, total);
    }

    /// <summary>
    /// Доверенности. Ищут по фамилии представителя и по фразе из полномочий —
    /// «вправе ли он подписывать договоры аренды».
    /// </summary>
    private async Task<(List<SearchHitDto>, int)> SearchPoaAsync(
        SearchRequest request, string? tsQuery, int take)
    {
        if (!_currentUser.HasPermission(Users.Models.PermissionCode.ViewPowersOfAttorney))
            return ([], 0);

        var query = _db.PowersOfAttorney
            .Include(p => p.HolderUnit)
            .AsNoTracking()
            .AsQueryable();

        if (request.OrgUnitId is { } unitId)
            query = query.Where(p => p.HolderUnitId == unitId);

        if (request.From is { } from)
            query = query.Where(p => p.IssuedOn >= from);

        if (request.To is { } to)
            query = query.Where(p => p.IssuedOn <= to);

        if (tsQuery is not null)
            query = query.Where(p => p.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", tsQuery)));

        var total = await query.CountAsync();

        var rows = await query
            .OrderByDescending(p => p.IssuedOn).ThenByDescending(p => p.Id)
            .Take(take)
            .Select(p => new
            {
                p.Id, p.RegNumber, p.HolderName, p.Powers, p.Status, p.ValidTo, p.CreatedAt,
                Unit = p.HolderUnit!.TitleRu,
            })
            .ToListAsync();

        var hits = rows.Select(r => new SearchHitDto
        {
            Scope = SearchScope.PowerOfAttorney,
            ScopeTitle = SearchScopeMap.Title(SearchScope.PowerOfAttorney),
            Id = r.Id,
            RegNumber = r.RegNumber,
            Title = $"Доверенность на {r.HolderName}",
            Snippet = Snippet(r.Powers, request.Query),
            StatusTitle = PoaStatusTitle(r.Status, r.ValidTo),
            OrgUnitTitle = r.Unit,
            CreatedAt = r.CreatedAt,
            Url = $"/poa/{r.Id}",
        }).ToList();

        return (hits, total);
    }

    private static string VndStatusTitle(Documents.VND.Models.VndStatus status) => status switch
    {
        Documents.VND.Models.VndStatus.Active => "Действует",
        Documents.VND.Models.VndStatus.OnActualization => "На актуализации",
        Documents.VND.Models.VndStatus.Review => "На согласовании",
        Documents.VND.Models.VndStatus.Consolidation => "На утверждении",
        Documents.VND.Models.VndStatus.Archived => "В архиве",
        Documents.VND.Models.VndStatus.Draft => "Черновик",
        _ => status.ToString(),
    };

    private static string PoaStatusTitle(
        PowerOfAttorney.Models.PoaStatus status, DateOnly validTo) => status switch
    {
        PowerOfAttorney.Models.PoaStatus.Active => $"Действует по {validTo:dd.MM.yyyy}",
        PowerOfAttorney.Models.PoaStatus.Revoked => "Отозвана",
        PowerOfAttorney.Models.PoaStatus.Expired => "Срок истёк",
        PowerOfAttorney.Models.PoaStatus.Draft => "Проект",
        PowerOfAttorney.Models.PoaStatus.OnApproval => "На подписании",
        _ => status.ToString(),
    };

    // ── общее ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Собирает tsquery из пользовательской строки. Слова склеиваются через «И» и
    /// получают префиксный поиск: в строке поиска набирают начало слова, а не
    /// словоформу целиком. Всё, кроме букв и цифр, отбрасывается — иначе спецсимвол
    /// уронил бы разбор запроса на стороне базы.
    /// </summary>
    private static string? BuildTsQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var terms = TermSplitter.Split(query)
            .Where(t => t.Length > 0)
            .Take(10)
            .Select(t => t.ToLowerInvariant() + ":*")
            .ToList();

        return terms.Count == 0 ? null : string.Join(" & ", terms);
    }

    /// <summary>
    /// Человеческое название статуса. Коды статусов свои у каждого контура и хранятся
    /// строками в карточке документа; в выдаче показывать «OnApproval» нельзя — читают
    /// её не разработчики.
    /// </summary>
    private static string StatusTitle(string? code) => code switch
    {
        "Draft" => "Черновик",
        "PendingRegistration" => "Ждёт регистрации",
        "Registered" => "Зарегистрирована",
        "OnApproval" => "На согласовании",
        "OnRevision" => "На доработке",
        "OnAddresseeDecision" => "На решении адресата",
        "OnExecution" => "На исполнении",
        "Approved" => "Согласовано",
        "InProcurement" => "В процедуре закупки",
        "Executed" => "Исполнена",
        "Completed" => "Завершён",
        "Signed" => "Подписан",
        "Terminated" => "Расторгнут",
        "Rejected" => "Отклонено",
        "Withdrawn" => "Отозвана",
        "Cancelled" => "Отменено",
        "Archived" => "В архиве",
        null => "—",
        _ => code,
    };

    /// <summary>Фрагмент вокруг найденного слова — чтобы в выдаче было видно, за что нашлось.</summary>
    private static string? Snippet(string? text, string? query)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var needle = TermSplitter.Split(query ?? string.Empty).FirstOrDefault(t => t.Length > 2);
        var position = needle is null
            ? -1
            : text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);

        if (position < 0)
            return text.Length <= SnippetRadius * 2 ? text : text[..(SnippetRadius * 2)] + "…";

        var start = Math.Max(0, position - SnippetRadius);
        var length = Math.Min(text.Length - start, SnippetRadius * 2);

        var builder = new StringBuilder();
        if (start > 0) builder.Append('…');
        builder.Append(text.Substring(start, length));
        if (start + length < text.Length) builder.Append('…');

        return builder.ToString();
    }
}
