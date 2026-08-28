using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Models;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IProcurementRequestService
{
    Task<PagedResult<ProcurementListItemDto>> SearchAsync(ProcurementSearchRequest request, int currentUserId);
    Task<ProcurementCountersDto> CountersAsync(int currentUserId);
    Task<ProcurementCardDto> GetAsync(int id);
    Task<ProcurementCardDto> CreateAsync(ProcurementCreateRequest request, int actorUserId);
    Task<ProcurementCardDto> SubmitAsync(int id, int actorUserId);
}

/// <summary>
/// Реестр заявок на закупку (PRC-01/03). Заявка ложится на единую карточку документа,
/// а способ, состав согласования и орган утверждения берутся у Матрицы полномочий
/// в момент создания и сохраняются в заявке — чтобы правка порогов задним числом
/// не меняла решение по уже запущенной закупке.
/// </summary>
public class ProcurementRequestService : IProcurementRequestService
{
    /// <summary>Тип связи «записка → заявка на закупку», заводится контуром СЗ.</summary>
    private const string SzLinkType = "SzToProcurement";

    /// <summary>Шаблон регистрационного номера заявки на закупку (GEN-09).</summary>
    private const string NumberPattern = "ЗК-{year}-{seq:D4}";

    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;
    private readonly IAuthorityMatrixService _matrix;
    private readonly IProcurementRouteService _routes;
    private readonly IBankClock _clock;

    public ProcurementRequestService(
        DelosferaDbContext db, IDocumentService documents, IAuditService audit,
        IAuthorityMatrixService matrix, IProcurementRouteService routes, IBankClock clock)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
        _matrix = matrix;
        _routes = routes;
        _clock = clock;
    }

    public async Task<PagedResult<ProcurementListItemDto>> SearchAsync(
        ProcurementSearchRequest request, int currentUserId)
    {
        var q = BaseQuery();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = $"%{request.Query.Trim()}%";
            q = q.Where(r =>
                EF.Functions.ILike(r.Subject, term) ||
                (r.Document!.RegNumber != null && EF.Functions.ILike(r.Document.RegNumber, term)));
        }

        if (request.Statuses is {Count: > 0})
            q = q.Where(r => request.Statuses.Contains(r.Document!.StatusCode));

        if (request.MethodId is { } methodId)
            q = q.Where(r => r.MethodId == methodId);

        if (request.MineOnly == true)
            q = q.Where(r => r.Document!.AuthorId == currentUserId);

        if (request.AmountFrom is { } from)
            q = q.Where(r => r.Amount >= from);

        if (request.AmountTo is { } to)
            q = q.Where(r => r.Amount <= to);

        var total = await q.CountAsync();
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);

        var rows = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new ProcurementListItemDto
            {
                Id = r.Id,
                DocumentId = r.DocumentId,
                RegNumber = r.Document!.RegNumber,
                Subject = r.Subject,
                StatusCode = r.Document.StatusCode,
                MethodShortTitle = r.Method!.ShortTitleRu,
                Amount = r.Amount,
                IsAffiliated = r.IsAffiliated,
                HasBudget = r.HasBudget,
                InitiatorName = r.Document.Author!.FullName,
                InitiatorUnit = r.InitiatorUnit!.TitleRu,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync();

        await FillSourceAsync(rows);

        return new PagedResult<ProcurementListItemDto>
        {
            Items = rows,
            Total = total,
            Page = page,
            PageSize = size,
        };
    }

    public async Task<ProcurementCountersDto> CountersAsync(int currentUserId)
    {
        var byStatus = await BaseQuery()
            .GroupBy(r => r.Document!.StatusCode)
            .Select(g => new {Status = g.Key, Count = g.Count()})
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        int Count(string status) => byStatus.TryGetValue(status, out var n) ? n : 0;

        return new ProcurementCountersDto
        {
            All = byStatus.Values.Sum(),
            Drafts = await BaseQuery()
                .CountAsync(r => r.Document!.StatusCode == ProcurementStatus.Draft
                                 && r.Document.AuthorId == currentUserId),
            OnApproval = Count(ProcurementStatus.OnApproval),
            InProcurement = Count(ProcurementStatus.InProcurement),
            Completed = Count(ProcurementStatus.Completed),
            ByStatus = byStatus,
        };
    }

    public async Task<ProcurementCardDto> GetAsync(int id)
    {
        var r = await LoadAsync(id);
        return await BuildCardAsync(r);
    }

    public async Task<ProcurementCardDto> CreateAsync(ProcurementCreateRequest request, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Укажите предмет закупки");

        if (request.Amount <= 0)
            throw new ArgumentException("Укажите ориентировочную сумму закупки");

        // Окно объявления задаётся целиком или не задаётся вовсе: одна дата без второй
        // не говорит ни о сроке приёма предложений, ни о дате публикации.
        if (request.AnnouncementFrom is null != request.AnnouncementTo is null)
            throw new ArgumentException("Укажите обе даты объявления закупки — «с» и «по»");

        if (request.AnnouncementFrom is { } from && request.AnnouncementTo is { } to && to < from)
            throw new ArgumentException("Дата окончания объявления раньше даты начала");

        // Способ и состав согласования определяет матрица; инициатор может настоять
        // на прямом заключении, но тогда обязательно обоснование (п. 6.6 Положения).
        var resolved = await _matrix.ResolveAsync(new MatrixResolveRequest
        {
            Amount = request.Amount,
            IsAffiliated = request.IsAffiliated,
            PreferredMethod = ParseMethod(request.PreferredMethod),
        });

        if (resolved.RequiresJustification && string.IsNullOrWhiteSpace(request.MethodJustification))
            throw new ArgumentException(
                $"Для способа «{resolved.MethodTitle}» обязательно обоснование применения метода");

        var method = await _db.ProcurementMethods.FirstAsync(m => m.Code.ToString() == resolved.MethodCode);

        // Заявка по служебной записке уже имеет документ-заготовку: переиспользуем его,
        // чтобы связь СЗ → закупка и нумерация остались прежними (GEN-05).
        var document = request.ExistingDocumentId is { } existingId
            ? await _db.Documents.FirstOrDefaultAsync(d => d.Id == existingId && d.Type == DocumentType.Procurement)
              ?? throw new InvalidOperationException("Документ-заготовка закупки не найден")
            : await _documents.CreateAsync(
                DocumentType.Procurement, request.Subject.Trim(), actorUserId, ProcurementStatus.Draft);

        if (await _db.ProcurementRequests.AnyAsync(x => x.DocumentId == document.Id))
            throw new InvalidOperationException("По этому документу заявка на закупку уже заведена");

        // Позиция Плана выбирается из справочника, поэтому её проверяем: код и
        // предмет для карточки берём из самой позиции, а не с чужих слов.
        var planItem = request.PlanItemId is { } planItemId
            ? await _db.ProcurementPlanItems
                  .Include(i => i.Plan)
                  .FirstOrDefaultAsync(i => i.Id == planItemId)
              ?? throw new KeyNotFoundException("Позиция Плана закупок не найдена")
            : null;

        if (planItem is not null && planItem.Plan!.Status != PlanStatus.Approved)
            throw new InvalidOperationException(
                "Ссылаться можно только на позицию утверждённого Плана: в черновике позиции ещё меняются");

        // Инициирующее подразделение — подразделение инициатора, если он его не
        // переопределил. Раньше поле оставалось пустым, и заявка упиралась в
        // «Не указано инициирующее подразделение» на ровном месте.
        var initiatorUnitId = request.InitiatorUnitId
                              ?? await _db.Users
                                  .Where(u => u.Id == actorUserId)
                                  .Select(u => u.OrgUnitId)
                                  .FirstOrDefaultAsync();

        var curatorUserId = request.CuratorUserId
                            ?? (initiatorUnitId is { } unitId
                                ? await _db.OrganizationUnits
                                    .Where(u => u.Id == unitId)
                                    .Select(u => u.CuratorUserId)
                                    .FirstOrDefaultAsync()
                                : null);

        var entity = new ProcurementRequest
        {
            DocumentId = document.Id,
            Subject = request.Subject.Trim(),
            Justification = request.Justification?.Trim(),
            SubjectKind = request.SubjectKind,
            Amount = request.Amount,
            IsAffiliated = request.IsAffiliated,
            HasBudget = request.HasBudget,
            PlanItemId = planItem?.Id,
            PlanItem = planItem is null ? null : $"{planItem.Code} — {planItem.Subject}",
            HasSpecification = request.HasSpecification,
            AnnouncementFrom = request.AnnouncementFrom,
            AnnouncementTo = request.AnnouncementTo,
            InitiatorUnitId = initiatorUnitId,
            CuratorUserId = curatorUserId,
            MethodId = method.Id,
            MatrixRuleId = resolved.RuleId,
            ApprovalChain = resolved.ApprovalChain,
            ApprovalAuthority = resolved.ApprovalAuthority,
            MethodJustification = request.MethodJustification?.Trim(),
            ProtocolRequired = resolved.ProtocolRequired,
        };

        _db.ProcurementRequests.Add(entity);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementRequest", entity.Id, "Created", actorUserId, new
        {
            entity.Subject,
            entity.Amount,
            Method = resolved.MethodTitle,
            resolved.ApprovalChain,
            Authority = resolved.ApprovalAuthorityTitle,
        });

        return await BuildCardAsync(await LoadAsync(entity.Id));
    }

    /// <summary>
    /// Похожие закупки того же подразделения за последние два месяца (п. 10.3).
    ///
    /// Положение требует возвращать заявку на консолидацию, если аналогичную
    /// продукцию закупают дважды и более за два месяца: так закупка дробится и
    /// каждая часть проходит по более мягкому порогу, чем целое. Отследить это
    /// глазами нельзя — заявки подают разные люди в разные недели.
    ///
    /// Сходство ищется поисковым вектором по предмету: точного признака
    /// «аналогичной продукции» Положение не даёт, а совпадение слов в предмете —
    /// то немногое, на что можно опереться. Поэтому это подсказка организатору,
    /// а не запрет: решение о консолидации принимает он.
    /// </summary>
    private async Task<List<SimilarRequestDto>> FindSimilarAsync(ProcurementRequest request)
    {
        if (request.InitiatorUnitId is null || string.IsNullOrWhiteSpace(request.Subject))
            return [];

        var since = _clock.Today.AddMonths(-2).ToDateTime(TimeOnly.MinValue).ToUniversalTime();

        var запрос = BuildSimilarityQuery(request.Subject);
        if (запрос is null) return [];

        return await _db.ProcurementRequests
            .AsNoTracking()
            .Include(r => r.Document)
            .Where(r => r.Id != request.Id
                        && r.InitiatorUnitId == request.InitiatorUnitId
                        && r.SubjectKind == request.SubjectKind
                        && r.CreatedAt >= since
                        && r.Document!.StatusCode != ProcurementStatus.Cancelled
                        && r.Document.StatusCode != ProcurementStatus.Rejected
                        && r.SearchVector!.Matches(EF.Functions.ToTsQuery("russian", запрос)))
            .OrderByDescending(r => r.CreatedAt)
            .Take(10)
            .Select(r => new SimilarRequestDto
            {
                Id = r.Id,
                RegNumber = r.Document!.RegNumber,
                Subject = r.Subject,
                Amount = r.Amount,
                CreatedAt = r.CreatedAt,
                StatusCode = r.Document.StatusCode,
            })
            .ToListAsync();
    }

    /// <summary>
    /// Полнотекстовый запрос для поиска похожих закупок.
    ///
    /// Слова соединяются через ИЛИ, а не И. Дробление выглядит как «Картриджи для
    /// принтеров, партия 1» и «…партия 2»: требуя совпадения всех слов, мы не
    /// нашли бы ровно тот случай, ради которого ищем. Короткие слова отбрасываем —
    /// предлоги и номера партий только шумят.
    ///
    /// Возвращает null, если опереться не на что: искать по одному предлогу
    /// значит вывалить организатору весь реестр.
    /// </summary>
    public static string? BuildSimilarityQuery(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return null;

        var слова = subject
            .Split([' ', ',', ';', '.', '(', ')', '«', '»', '"', '/', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length >= 4 && w.Any(char.IsLetter))
            .Distinct()
            .Take(8)
            .ToList();

        return слова.Count == 0 ? null : string.Join(" | ", слова);
    }

    public async Task<ProcurementCardDto> SubmitAsync(int id, int actorUserId)
    {
        var entity = await LoadAsync(id);
        var card = await BuildCardAsync(entity);

        if (entity.Document!.StatusCode != ProcurementStatus.Draft
            && entity.Document.StatusCode != ProcurementStatus.OnRevision)
            throw new InvalidOperationException("На согласование отправляется черновик или заявка на доработке");

        if (card.Blockers.Count > 0)
            throw new InvalidOperationException(string.Join("; ", card.Blockers));

        // Номер присваивается при запуске согласования: до этого заявка — черновик
        // инициатора, а по номеру её уже ищут согласующие и Сектор закупок (GEN-09).
        var number = entity.Document.RegNumber
                     ?? await _documents.RegisterAsync(entity.DocumentId, "Procurement", "global", NumberPattern, actorUserId);

        // Маршрут строится до смены статуса: если согласующих определить не удалось,
        // заявка должна остаться черновиком, а не повиснуть «на согласовании» без задач.
        var route = await _routes.StartAsync(entity, actorUserId);
        entity.Document.CurrentRouteInstanceId = route.Id;

        await _documents.ChangeStatusAsync(entity.DocumentId, ProcurementStatus.OnApproval, actorUserId);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("ProcurementRequest", entity.Id, "SubmittedForApproval", actorUserId, new
        {
            number,
            entity.ApprovalChain,
            routeInstanceId = route.Id,
        });

        return await BuildCardAsync(await LoadAsync(id));
    }

    private IQueryable<ProcurementRequest> BaseQuery() =>
        _db.ProcurementRequests
            .Include(r => r.Document).ThenInclude(d => d!.Author)
            .Include(r => r.Method)
            .Include(r => r.InitiatorUnit)
            .Include(r => r.CuratorUser)
            .AsQueryable();

    private async Task<ProcurementRequest> LoadAsync(int id) =>
        await BaseQuery().FirstOrDefaultAsync(r => r.Id == id)
        ?? throw new KeyNotFoundException($"Заявка на закупку {id} не найдена");

    private async Task<ProcurementCardDto> BuildCardAsync(ProcurementRequest r)
    {
        var card = new ProcurementCardDto
        {
            Id = r.Id,
            DocumentId = r.DocumentId,
            RegNumber = r.Document!.RegNumber,
            Subject = r.Subject,
            StatusCode = r.Document.StatusCode,
            Justification = r.Justification,
            SubjectKind = r.SubjectKind,
            SubjectKindTitle = SubjectKindTitle(r.SubjectKind),
            Amount = r.Amount,
            IsAffiliated = r.IsAffiliated,
            HasBudget = r.HasBudget,
            PlanItemId = r.PlanItemId,
            PlanItem = r.PlanItem,
            HasSpecification = r.HasSpecification,
            AnnouncementFrom = r.AnnouncementFrom,
            AnnouncementTo = r.AnnouncementTo,
            InitiatorName = r.Document.Author?.FullName,
            InitiatorUnit = r.InitiatorUnit?.TitleRu,
            CuratorName = r.CuratorUser?.FullName,
            MethodTitle = r.Method!.TitleRu,
            MethodShortTitle = r.Method.ShortTitleRu,
            MethodJustification = r.MethodJustification,
            ApprovalChain = r.ApprovalChain,
            ApprovalAuthorityTitle = AuthorityTitle(r.ApprovalAuthority),
            ProtocolRequired = r.ProtocolRequired,
            MinProposals = r.Method.MinProposals,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            RouteInstanceId = r.Document.CurrentRouteInstanceId,
        };

        // Конкурс и договор нужны карточке, чтобы показать обеспечения и претензии:
        // ГОКЗ относится к конкурсу, ГОИД и претензии — к договору.
        card.TenderId = await _db.Tenders
            .Where(t => t.RequestId == r.Id && t.Status != TenderStatus.Cancelled)
            .OrderByDescending(t => t.Id)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync();

        card.ContractId = await _db.ProcurementContracts
            .Where(c => c.RequestId == r.Id && c.Status != ContractStatus.Terminated)
            .OrderByDescending(c => c.Id)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();

        var link = await _db.DocumentLinks
            .Where(l => l.ToDocumentId == r.DocumentId && l.LinkType == SzLinkType)
            .Select(l => new {l.FromDocumentId, RegNumber = l.FromDocument!.RegNumber})
            .FirstOrDefaultAsync();

        if (link is not null)
        {
            card.SourceSzId = link.FromDocumentId;
            card.SourceSzRegNumber = link.RegNumber;
        }

        // Похожие закупки ищем только пока заявка ещё в работе: по завершённой
        // консолидировать уже нечего, а запрос стоит полнотекстового поиска.
        if (r.Document.StatusCode is ProcurementStatus.Draft or ProcurementStatus.OnRevision
                                  or ProcurementStatus.OnApproval)
            card.SimilarRequests = await FindSimilarAsync(r);

        FillBlockers(card, r);
        return card;
    }

    private static void FillBlockers(ProcurementCardDto card, ProcurementRequest r)
    {
        if (!r.HasSpecification)
            card.Blockers.Add("Не приложено техническое задание (спецификация)");

        if (r.InitiatorUnitId is null)
            card.Blockers.Add("Не указано инициирующее подразделение");

        if (string.IsNullOrWhiteSpace(r.Justification))
            card.Blockers.Add("Не заполнено обоснование необходимости закупки");

        if (!r.HasBudget && string.IsNullOrWhiteSpace(r.PlanItem))
            card.Blockers.Add("Закупка вне бюджета и без позиции Плана закупок — требуется ветка внеплановой закупки (PRC-03)");

        if (r.Method!.RequiresJustification && string.IsNullOrWhiteSpace(r.MethodJustification))
            card.Blockers.Add("Не заполнено обоснование применения способа закупки");
    }

    private async Task FillSourceAsync(List<ProcurementListItemDto> rows)
    {
        if (rows.Count == 0) return;

        var docIds = rows.Select(x => x.DocumentId).ToList();
        var links = await _db.DocumentLinks
            .Where(l => docIds.Contains(l.ToDocumentId) && l.LinkType == SzLinkType)
            .Select(l => new {l.ToDocumentId, RegNumber = l.FromDocument!.RegNumber})
            .ToListAsync();

        foreach (var row in rows)
            row.SourceSzRegNumber = links.FirstOrDefault(l => l.ToDocumentId == row.DocumentId)?.RegNumber;
    }

    private static ProcurementMethodCode? ParseMethod(string? code) =>
        Enum.TryParse<ProcurementMethodCode>(code, ignoreCase: true, out var parsed) ? parsed : null;

    private static string SubjectKindTitle(ProcurementSubjectKind kind) => kind switch
    {
        ProcurementSubjectKind.HouseholdGoods => "Хозяйственные товары",
        ProcurementSubjectKind.SpecificGoods => "Специфичный товар",
        ProcurementSubjectKind.GoodsWithInstallation => "Товар с установкой и вводом в эксплуатацию",
        ProcurementSubjectKind.Works => "Работы",
        ProcurementSubjectKind.Services => "Услуги",
        _ => "Товары",
    };

    private static string AuthorityTitle(ApprovalAuthority authority) => authority switch
    {
        ApprovalAuthority.Curator => "Куратор",
        ApprovalAuthority.Board => "Правление",
        ApprovalAuthority.SupervisoryBoard => "Совет директоров",
        ApprovalAuthority.Shareholders => "Общее собрание акционеров",
        _ => "Не требуется",
    };
}
