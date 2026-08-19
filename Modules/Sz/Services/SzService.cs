using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Models;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>Служебные записки (контур СЗ): черновик → регистрация → реестр.</summary>
public interface ISzService
{
    Task<PagedResult<SzListItem>> SearchAsync(SzSearchRequest request, int currentUserId);
    Task<SzDetails?> GetAsync(int id);
    Task<SzDetails> CreateDraftAsync(SzSaveRequest request, int authorId);
    Task<SzDetails> UpdateDraftAsync(int id, SzSaveRequest request, int actorUserId);
    Task DeleteDraftAsync(int id, int actorUserId);

    /// <summary>Отправить записку: черновик уходит на регистрацию делопроизводством.</summary>
    Task<SzDetails> SubmitAsync(int id, int actorUserId);

    /// <summary>Состав согласующих и порядок их прохождения (последовательно или параллельно).</summary>
    Task<SzDetails> SetApproversAsync(int id, IReadOnlyList<int> userIds, bool parallel, int actorUserId);

    /// <summary>Решение адресата по существу вопроса — доступно только ему.</summary>
    Task<SzDetails> DecideAsAddresseeAsync(int id, string decision, int actorUserId);

    /// <summary>
    /// Зарегистрировать: присвоить номер, дату и срок исполнения, запустить маршрут
    /// согласования (SZ-01). Шаблон берётся из вида записки или указывается явно.
    /// </summary>
    Task<SzDetails> RegisterAsync(int id, int actorUserId, int? templateId = null);

    /// <summary>
    /// Отозвать записку с согласования с обоснованием (SZ): маршрут прерывается,
    /// записка возвращается автору в черновик, повторная отправка пойдёт с первого этапа.
    /// </summary>
    Task<SzDetails> WithdrawAsync(int id, string reason, int actorUserId);

    /// <summary>Записки, где текущий пользователь — активный согласующий («СЗ, согласую я»).</summary>
    Task<PagedResult<SzListItem>> InboxAsync(int currentUserId, int page, int pageSize);
}

public class SzService : ISzService
{
    /// <summary>Шаблон регистрационного номера СЗ (GEN-09).</summary>
    private const string NumberPattern = "СЗ-{year}-{seq:D4}";

    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;
    private readonly IRouteEngine _routeEngine;

    public SzService(
        DelosferaDbContext db, IDocumentService documents, IAuditService audit, IRouteEngine routeEngine)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
        _routeEngine = routeEngine;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<PagedResult<SzListItem>> SearchAsync(SzSearchRequest request, int currentUserId)
    {
        var query = BaseQuery();

        if (request.MineOnly)
            query = query.Where(x => x.Document!.AuthorId == currentUserId);
        else
            // Чужие черновики не показываются в общем реестре — они ещё не документы.
            query = query.Where(x => x.Document!.StatusCode != SzStatus.Draft
                                  || x.Document!.AuthorId == currentUserId);

        var statuses = request.Statuses.Count > 0
            ? request.Statuses.Where(s => SzStatus.All.Contains(s)).ToArray()
            : SzStatus.Active;
        query = query.Where(x => statuses.Contains(x.Document!.StatusCode));

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var q = request.Query.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Document!.Title, $"%{q}%") ||
                (x.Document!.RegNumber != null && EF.Functions.ILike(x.Document!.RegNumber!, $"%{q}%")) ||
                (x.Body != null && EF.Functions.ILike(x.Body, $"%{q}%")));
        }

        if (request.KindIds.Count > 0)
            query = query.Where(x => request.KindIds.Contains(x.KindId));

        if (request.AuthorId is int author)
            query = query.Where(x => x.Document!.AuthorId == author);

        if (request.CorrespondentUnitId is int unit)
            query = query.Where(x => x.CorrespondentUnitId == unit);

        if (request.RubricId is int rubric)
            query = query.Where(x => x.Rubrics.Any(r => r.Id == rubric));

        if (request.RegisteredFrom is DateOnly from)
            query = query.Where(x => x.RegisteredOn >= from);

        if (request.RegisteredTo is DateOnly to)
            query = query.Where(x => x.RegisteredOn <= to);

        if (request.OverdueOnly)
        {
            var today = Today;
            query = query.Where(x => x.DueDate != null && x.DueDate < today
                                  && x.Document!.StatusCode != SzStatus.Executed);
        }

        var total = await query.CountAsync();

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;

        var items = await query
            .OrderByDescending(x => x.RegisteredOn ?? DateOnly.MaxValue)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<SzListItem>
        {
            Items = items.Select(ToListItem).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SzDetails?> GetAsync(int id)
    {
        var sz = await BaseQuery().FirstOrDefaultAsync(x => x.Id == id);
        return sz is null ? null : ToDetails(sz);
    }

    public async Task<SzDetails> CreateDraftAsync(SzSaveRequest request, int authorId)
    {
        var kind = await _db.SzKinds.FirstOrDefaultAsync(k => k.Id == request.KindId)
            ?? throw new KeyNotFoundException("Вид служебной записки не найден");

        var document = await _documents.CreateAsync(DocumentType.Sz, request.Title, authorId, SzStatus.Draft);
        document.IsPaperCarrier = request.IsPaperCarrier
            ?? await NeedsPaperAsync(kind, request.CorrespondentUnitId);

        var author = await _db.Users.FirstOrDefaultAsync(u => u.Id == authorId);

        var sz = new SzDocument
        {
            DocumentId = document.Id,
            KindId = kind.Id,
            AuthorUnitId = author?.OrgUnitId
        };
        ApplyFields(sz, request);

        _db.SzDocuments.Add(sz);
        await ApplyRubricsAsync(sz, request.RubricIds);
        await _db.SaveChangesAsync();

        await ReplaceApproversAsync(sz.Id, request.ApproverUserIds);

        await _audit.LogAsync("Sz", sz.Id, "Created", authorId, new { kind = kind.TitleRu });

        return (await GetAsync(sz.Id))!;
    }

    public async Task<SzDetails> UpdateDraftAsync(int id, SzSaveRequest request, int actorUserId)
    {
        var sz = await _db.SzDocuments
            .Include(x => x.Document)
            .Include(x => x.Rubrics)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Служебная записка не найдена");

        // Правка полей разрешена, пока записка не ушла дальше автора.
        if (sz.Document!.StatusCode is not (SzStatus.Draft or SzStatus.OnRevision))
            throw new InvalidOperationException("Изменять можно только черновик или записку на доработке");

        sz.Document.Title = request.Title;
        if (request.IsPaperCarrier is bool paper)
        {
            sz.Document.IsPaperCarrier = paper;
        }
        else if (sz.KindId != request.KindId || sz.CorrespondentUnitId != request.CorrespondentUnitId)
        {
            // Автор сменил вид или адресата, а признак носителя вручную не трогал —
            // пересчитываем, иначе записка «бумажному» адресату уедет электронной.
            var kind = await _db.SzKinds.FirstOrDefaultAsync(k => k.Id == request.KindId)
                ?? throw new KeyNotFoundException("Вид служебной записки не найден");
            sz.Document.IsPaperCarrier = await NeedsPaperAsync(kind, request.CorrespondentUnitId);
        }
        sz.KindId = request.KindId;
        ApplyFields(sz, request);
        await ApplyRubricsAsync(sz, request.RubricIds);
        await ReplaceApproversAsync(sz.Id, request.ApproverUserIds);

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Sz", sz.Id, "Updated", actorUserId);

        return (await GetAsync(sz.Id))!;
    }

    public async Task DeleteDraftAsync(int id, int actorUserId)
    {
        var sz = await _db.SzDocuments.Include(x => x.Document)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Служебная записка не найдена");

        if (sz.Document!.StatusCode != SzStatus.Draft)
            throw new InvalidOperationException("Удалить можно только черновик");

        // Карточка документа уходит вместе с запиской (каскад по DocumentId).
        _db.SzDocuments.Remove(sz);
        _db.Documents.Remove(sz.Document);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", id, "Deleted", actorUserId);
    }

    public async Task<SzDetails> SubmitAsync(int id, int actorUserId)
    {
        var sz = await _db.SzDocuments.Include(x => x.Document)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Служебная записка не найдена");

        if (sz.Document!.StatusCode is not (SzStatus.Draft or SzStatus.OnRevision))
            throw new InvalidOperationException("Отправить можно только черновик или записку с доработки");

        if (string.IsNullOrWhiteSpace(sz.Body))
            throw new InvalidOperationException("Заполните текст служебной записки");

        if (sz.AddresseeUserId is null && sz.CorrespondentUnitId is null)
            throw new InvalidOperationException("Укажите адресата записки");

        var approvers = await _db.SzApprovers
            .Where(a => a.SzDocumentId == sz.Id)
            .OrderBy(a => a.Order)
            .Select(a => a.UserId)
            .ToListAsync();

        if (approvers.Count == 0)
        {
            // Согласующих автор не выбрал — записка идёт прежним путём: делопроизводитель
            // регистрирует её и запускает маршрут по шаблону вида (SZ-01).
            await _documents.ChangeStatusAsync(sz.DocumentId, SzStatus.PendingRegistration, actorUserId);
            await _audit.LogAsync("Sz", sz.Id, "Submitted", actorUserId);

            return (await GetAsync(sz.Id))!;
        }

        // Автор назвал согласующих сам — отдельная регистрация делопроизводителем
        // здесь ничего не решает, но номер и срок записка получить обязана: без них
        // на неё нельзя сослаться и по ней нельзя посчитать просрочку.
        await RegisterAndStartAsync(sz, approvers, actorUserId);

        return (await GetAsync(sz.Id))!;
    }

    /// <summary>
    /// Присвоить записке номер и срок и запустить согласование по выбранным автором
    /// согласующим.
    /// </summary>
    private async Task RegisterAndStartAsync(SzDocument sz, List<int> approvers, int actorUserId)
    {
        var kind = sz.Kind ?? await _db.SzKinds.FirstOrDefaultAsync(k => k.Id == sz.KindId);

        var number = await _documents.RegisterAsync(
            sz.DocumentId, "Sz", "global", NumberPattern, actorUserId);

        sz.RegisteredOn = Today;
        sz.RegisteredByUserId = actorUserId;
        sz.DueDate = sz.RegisteredOn.Value.AddDays(kind?.ExecutionDays ?? 14);
        sz.ApprovalRounds++;

        // Подписант, если он назван в карточке, замыкает маршрут отдельным этапом:
        // раньше поле заполнялось, а действия под него не было — человека назначали,
        // и на этом всё заканчивалось.
        var instance = await _routeEngine.InstantiateForApproversAsync(
            sz.DocumentId, approvers, sz.ApprovalIsParallel, signerUserId: sz.SignerUserId);

        await _routeEngine.StartAsync(instance.Id, actorUserId);
        sz.Document!.CurrentRouteInstanceId = instance.Id;

        await _documents.ChangeStatusAsync(sz.DocumentId, SzStatus.Registered, actorUserId);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "Submitted", actorUserId, new
        {
            number,
            registeredOn = sz.RegisteredOn,
            dueDate = sz.DueDate,
            approvers = approvers.Count,
            parallel = sz.ApprovalIsParallel,
            routeInstanceId = instance.Id,
        });
    }

    public async Task<SzDetails> SetApproversAsync(int id, IReadOnlyList<int> userIds, bool parallel, int actorUserId)
    {
        var sz = await _db.SzDocuments.Include(x => x.Document)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Служебная записка не найдена");

        if (sz.Document!.StatusCode is not (SzStatus.Draft or SzStatus.OnRevision))
            throw new InvalidOperationException(
                "Состав согласующих меняется до отправки: маршрут уже запущен");

        var distinct = userIds.Distinct().ToList();

        var existing = await _db.SzApprovers.Where(a => a.SzDocumentId == id).ToListAsync();
        _db.SzApprovers.RemoveRange(existing);

        for (var i = 0; i < distinct.Count; i++)
        {
            _db.SzApprovers.Add(new SzApprover
            {
                SzDocumentId = id,
                UserId = distinct[i],
                Order = i + 1,
            });
        }

        sz.ApprovalIsParallel = parallel;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "ApproversChanged", actorUserId,
            new {count = distinct.Count, parallel});

        return (await GetAsync(sz.Id))!;
    }

    /// <summary>
    /// Решение адресата (поле «Кому»). Пишет только он сам: это ответ по существу
    /// вопроса, а не резолюция согласующего, и подменять его нельзя.
    /// </summary>
    public async Task<SzDetails> DecideAsAddresseeAsync(int id, string decision, int actorUserId)
    {
        var sz = await _db.SzDocuments.Include(x => x.Document)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Служебная записка не найдена");

        if (sz.AddresseeUserId is null)
            throw new InvalidOperationException("У записки не указан адресат");

        if (sz.AddresseeUserId != actorUserId)
            throw new UnauthorizedAccessException("Решение по записке выносит только её адресат");

        if (sz.Document!.StatusCode != SzStatus.OnAddresseeDecision)
            throw new InvalidOperationException(
                "Решение выносится после согласования: записка ещё не дошла до адресата");

        if (string.IsNullOrWhiteSpace(decision))
            throw new InvalidOperationException("Напишите решение по записке");

        sz.AddresseeDecision = decision.Trim();
        sz.AddresseeDecisionAt = DateTime.UtcNow;
        sz.AddresseeDecisionByUserId = actorUserId;

        // Задача адресата закрыта: ответ дан, и висеть в списке задач ей больше незачем.
        var task = await _db.WorkflowTasks.FirstOrDefaultAsync(t =>
            t.DocumentId == sz.DocumentId
            && t.Type == SzRouteCompletionHandler.AddresseeDecisionTask
            && t.State == WorkflowTaskState.Open);

        if (task is not null) task.State = WorkflowTaskState.Done;

        await _documents.ChangeStatusAsync(sz.DocumentId, SzStatus.OnExecution, actorUserId);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "AddresseeDecided", actorUserId);

        return (await GetAsync(sz.Id))!;
    }

    public async Task<SzDetails> RegisterAsync(int id, int actorUserId, int? templateId = null)
    {
        var sz = await _db.SzDocuments.Include(x => x.Document).Include(x => x.Kind)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Служебная записка не найдена");

        if (sz.Document!.StatusCode != SzStatus.PendingRegistration)
            throw new InvalidOperationException("Регистрируются только записки, ожидающие регистрации");

        // Согласующие, названные автором, важнее шаблона вида: шаблон — это умолчание
        // на случай, когда состав не выбран.
        var approvers = await _db.SzApprovers
            .Where(a => a.SzDocumentId == sz.Id)
            .OrderBy(a => a.Order)
            .Select(a => a.UserId)
            .ToListAsync();

        if (approvers.Count > 0 && templateId is null)
        {
            await RegisterAndStartAsync(sz, approvers, actorUserId);
            return (await GetAsync(sz.Id))!;
        }

        var routeTemplateId = templateId ?? sz.Kind?.RouteTemplateId
            ?? throw new InvalidOperationException(
                "Не задан маршрут согласования: укажите шаблон или пропишите его в виде записки");

        var number = await _documents.RegisterAsync(
            sz.DocumentId, "Sz", "global", NumberPattern, actorUserId);

        sz.RegisteredOn = Today;
        sz.RegisteredByUserId = actorUserId;
        // Норматив исполнения берётся из вида записки (по инструкции — 14 дней).
        sz.DueDate = sz.RegisteredOn.Value.AddDays(sz.Kind?.ExecutionDays ?? 14);
        sz.ApprovalRounds++;

        // Регистрация запускает согласование (SZ-01): маршрут строится из шаблона вида.
        var instance = await _routeEngine.InstantiateFromTemplateAsync(sz.DocumentId, routeTemplateId);

        // Подписант из карточки замыкает маршрут: шаблон описывает согласование,
        // а кто подписывает — решает автор записки.
        if (sz.SignerUserId is { } signer)
            await _routeEngine.AppendSigningStepAsync(instance.Id, signer);

        await _routeEngine.StartAsync(instance.Id, actorUserId);
        sz.Document.CurrentRouteInstanceId = instance.Id;

        await _documents.ChangeStatusAsync(sz.DocumentId, SzStatus.Registered, actorUserId);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "Registered", actorUserId,
            new { number, registeredOn = sz.RegisteredOn, dueDate = sz.DueDate, routeInstanceId = instance.Id });

        return (await GetAsync(sz.Id))!;
    }

    public async Task<SzDetails> WithdrawAsync(int id, string reason, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Укажите обоснование отзыва");

        var sz = await _db.SzDocuments.Include(x => x.Document)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException("Служебная записка не найдена");

        // Отзыв — право инициатора: согласующий прерывать чужой процесс не может.
        if (sz.Document!.AuthorId != actorUserId)
            throw new InvalidOperationException("Отозвать записку может только её автор");

        // Отзыв возможен на согласовании и доработке, но не когда записка уже исполняется.
        if (sz.Document.StatusCode is not (SzStatus.Registered or SzStatus.OnRevision or SzStatus.PendingRegistration))
            throw new InvalidOperationException("Отозвать можно записку на регистрации, согласовании или доработке");

        if (sz.Document.CurrentRouteInstanceId is int instanceId)
        {
            // История прошлых согласований остаётся: движок гасит задачи участников
            // и помечает экземпляр прерванным, не удаляя резолюции.
            await _routeEngine.InterruptAsync(instanceId, actorUserId);
            sz.Document.CurrentRouteInstanceId = null;
        }

        sz.WithdrawReason = reason.Trim();

        // По ТЗ отозванная записка возвращается автору черновиком; повтор пойдёт с первого этапа.
        await _documents.ChangeStatusAsync(sz.DocumentId, SzStatus.Draft, actorUserId);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Sz", sz.Id, "Withdrawn", actorUserId, new { reason = sz.WithdrawReason });

        return (await GetAsync(sz.Id))!;
    }

    public async Task<PagedResult<SzListItem>> InboxAsync(int currentUserId, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

        // Активные задачи согласования текущего пользователя.
        var documentIds = _db.RouteParticipants
            .Where(p => p.UserId == currentUserId && p.State == ParticipantState.Active)
            .Select(p => p.RouteStep!.RouteInstance!.DocumentId);

        var query = BaseQuery().Where(x => documentIds.Contains(x.DocumentId));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.DueDate ?? DateOnly.MaxValue)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<SzListItem>
        {
            Items = items.Select(ToListItem).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Бумажный носитель (SZ-PAP-01) включается по виду записки либо по адресату:
    /// подразделению может требоваться бумага независимо от вида.
    /// </summary>
    private async Task<bool> NeedsPaperAsync(SzKind kind, int? correspondentUnitId)
    {
        if (kind.IsPaperByDefault) return true;
        if (correspondentUnitId is not int unitId) return false;

        return await _db.OrganizationUnits.AsNoTracking()
            .AnyAsync(u => u.Id == unitId && u.RequiresPaperSz);
    }

    // --- вспомогательное ---

    private IQueryable<SzDocument> BaseQuery() =>
        _db.SzDocuments.AsNoTracking()
            .Include(x => x.Document)!.ThenInclude(d => d!.Author)
            .Include(x => x.Kind)
            .Include(x => x.HrKind)
            .Include(x => x.AuthorUnit)
            .Include(x => x.CorrespondentUnit)
            .Include(x => x.EmployeeUnit)
            .Include(x => x.TransferUnit)
            .Include(x => x.SignerUser)
            .Include(x => x.AddresseeUser)
            .Include(x => x.Approvers).ThenInclude(a => a.User)
            .Include(x => x.Rubrics);

    private static void ApplyFields(SzDocument sz, SzSaveRequest r)
    {
        sz.Body = r.Body;
        sz.CorrespondentUnitId = r.CorrespondentUnitId;
        sz.AddresseeUserId = r.AddresseeUserId;
        sz.ApprovalIsParallel = r.ApprovalIsParallel;
        sz.SignerUserId = r.SignerUserId;

        sz.HrKindId = r.HrKindId;
        sz.EmployeeName = r.EmployeeName;
        sz.EmployeeUnitId = r.EmployeeUnitId;
        sz.TransferUnitId = r.TransferUnitId;

        sz.HasBudget = r.HasBudget;
        sz.Amount = r.Amount;
        sz.TravelExpenses = r.TravelExpenses;

        sz.ExtraFields = r.ExtraFields is JsonElement extra
            ? JsonDocument.Parse(extra.GetRawText())
            : null;
    }

    /// <summary>
    /// Полная замена состава согласующих: порядок в списке и есть очерёдность
    /// прохождения маршрута.
    /// </summary>
    private async Task ReplaceApproversAsync(int szId, List<int> userIds)
    {
        var existing = await _db.SzApprovers.Where(a => a.SzDocumentId == szId).ToListAsync();
        _db.SzApprovers.RemoveRange(existing);

        var distinct = userIds.Distinct().ToList();
        for (var i = 0; i < distinct.Count; i++)
        {
            _db.SzApprovers.Add(new SzApprover
            {
                SzDocumentId = szId,
                UserId = distinct[i],
                Order = i + 1,
            });
        }

        await _db.SaveChangesAsync();
    }

    private async Task ApplyRubricsAsync(SzDocument sz, List<int> rubricIds)
    {
        sz.Rubrics.Clear();
        if (rubricIds.Count == 0) return;

        var rubrics = await _db.Rubrics.Where(r => rubricIds.Contains(r.Id)).ToListAsync();
        foreach (var rubric in rubrics) sz.Rubrics.Add(rubric);
    }

    private static SzListItem ToListItem(SzDocument x) => Fill(new SzListItem(), x);

    private static SzDetails ToDetails(SzDocument x)
    {
        var d = (SzDetails)Fill(new SzDetails(), x);

        d.FormKey = x.Kind?.FormKey ?? SzFormKey.Other;
        d.Body = x.Body;
        d.AuthorId = x.Document?.AuthorId ?? 0;
        d.AuthorUnitId = x.AuthorUnitId;
        d.AuthorUnit = x.AuthorUnit?.TitleRu;
        d.CorrespondentUnitId = x.CorrespondentUnitId;
        d.AddresseeUserId = x.AddresseeUserId;
        d.AddresseeUser = x.AddresseeUser?.FullName;
        d.ApprovalIsParallel = x.ApprovalIsParallel;
        d.AddresseeDecision = x.AddresseeDecision;
        d.AddresseeDecisionAt = x.AddresseeDecisionAt;
        d.Approvers = x.Approvers
            .OrderBy(a => a.Order)
            .Select(a => new SzApproverDto
            {
                UserId = a.UserId,
                FullName = a.User?.FullName ?? string.Empty,
                Position = a.User?.Position?.TitleRu,
                Order = a.Order,
            })
            .ToList();
        d.SignerUserId = x.SignerUserId;
        d.SignerUser = x.SignerUser?.FullName;
        d.RegisteredByUserId = x.RegisteredByUserId;
        d.RubricIds = x.Rubrics.Select(r => r.Id).ToList();
        d.Rubrics = x.Rubrics.Select(r => r.TitleRu).ToList();

        d.HrKindId = x.HrKindId;
        d.HrKind = x.HrKind?.TitleRu;
        d.EmployeeName = x.EmployeeName;
        d.EmployeeUnitId = x.EmployeeUnitId;
        d.EmployeeUnit = x.EmployeeUnit?.TitleRu;
        d.TransferUnitId = x.TransferUnitId;
        d.TransferUnit = x.TransferUnit?.TitleRu;

        d.HasBudget = x.HasBudget;
        d.Amount = x.Amount;
        d.TravelExpenses = x.TravelExpenses;

        d.ExtraFields = x.ExtraFields?.RootElement.Clone();
        d.CurrentRouteInstanceId = x.Document?.CurrentRouteInstanceId;
        d.WithdrawReason = x.WithdrawReason;
        d.ApprovalRounds = x.ApprovalRounds;
        d.ExecutionResolution = x.ExecutionResolution;
        d.ExecutionResolutionAt = x.ExecutionResolutionAt;
        d.DueDateExtensionReason = x.DueDateExtensionReason;
        d.DueDateExtensions = x.DueDateExtensions;
        d.ExecutionSummary = x.ExecutionSummary;
        d.ExecutedAt = x.ExecutedAt;

        return d;
    }

    private static SzListItem Fill(SzListItem item, SzDocument x)
    {
        var today = Today;
        var daysLeft = x.DueDate is DateOnly due ? due.DayNumber - today.DayNumber : (int?)null;

        item.Id = x.Id;
        item.DocumentId = x.DocumentId;
        item.RegNumber = x.Document?.RegNumber;
        item.Title = x.Document?.Title ?? string.Empty;
        item.StatusCode = x.Document?.StatusCode ?? SzStatus.Draft;
        item.KindId = x.KindId;
        item.Kind = x.Kind?.TitleRu ?? string.Empty;
        item.Author = x.Document?.Author?.FullName;
        item.CorrespondentUnit = x.CorrespondentUnit?.TitleRu;
        item.RegisteredOn = x.RegisteredOn;
        item.DueDate = x.DueDate;
        item.DaysLeft = daysLeft;
        item.IsOverdue = daysLeft < 0 && x.Document?.StatusCode != SzStatus.Executed;
        item.IsPaperCarrier = x.Document?.IsPaperCarrier ?? false;

        return item;
    }
}
