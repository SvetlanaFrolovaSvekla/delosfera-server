using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzExecutionService
{
    /// <summary>Резолюция руководителя: текст + поручения исполнителям.</summary>
    Task<List<SzAssignmentResponse>> ResolveAsync(int szId, SzResolutionRequest req, int actorUserId);

    /// <summary>Поручения по записке.</summary>
    Task<List<SzAssignmentResponse>> ListAsync(int szId);

    /// <summary>Очередь «Мои поручения»: незакрытые поручения текущего пользователя.</summary>
    Task<List<SzAssignmentResponse>> MyAsync(int currentUserId, bool includeClosed = false);

    /// <summary>Исполнитель сдаёт отчёт по поручению.</summary>
    Task<SzAssignmentResponse> ReportAsync(int assignmentId, string reportText, int actorUserId);

    /// <summary>Принять отчёт. Когда закрыто последнее поручение — записка исполнена.</summary>
    Task<SzAssignmentResponse> AcceptAsync(int assignmentId, int actorUserId);

    /// <summary>Вернуть отчёт исполнителю с причиной.</summary>
    Task<SzAssignmentResponse> ReturnAsync(int assignmentId, string reason, int actorUserId);

    /// <summary>Снять поручение.</summary>
    Task<SzAssignmentResponse> CancelAsync(int assignmentId, int actorUserId);

    /// <summary>Продлить срок исполнения записки с обоснованием (норматив 14 дней).</summary>
    Task ExtendDueDateAsync(int szId, DateOnly dueDate, string reason, int actorUserId);

    /// <summary>Закрыть записку вручную: итог исполнения обязателен.</summary>
    Task CompleteAsync(int szId, string summary, int actorUserId);
}

public class SzExecutionService : ISzExecutionService
{
    private readonly DelosferaDbContext _db;
    private readonly IDocumentService _documents;
    private readonly IAuditService _audit;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public SzExecutionService(DelosferaDbContext db, IDocumentService documents, IAuditService audit)
    {
        _db = db;
        _documents = documents;
        _audit = audit;
    }

    public async Task<List<SzAssignmentResponse>> ResolveAsync(
        int szId, SzResolutionRequest req, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            throw new InvalidOperationException("Укажите текст резолюции");
        if (req.Assignments.Count == 0)
            throw new InvalidOperationException("Резолюция без поручений смысла не имеет: добавьте исполнителей");
        if (req.Assignments.Count(x => x.IsPrimary) > 1)
            throw new InvalidOperationException("Ответственный исполнитель может быть только один");

        var sz = await LoadAsync(szId);

        // Поручения выдаются по согласованной записке: до этого исполнять нечего.
        if (sz.Document!.StatusCode != SzStatus.OnExecution)
            throw new InvalidOperationException("Резолюция выносится по записке, переданной на исполнение");

        var assigneeIds = req.Assignments.Select(x => x.AssigneeUserId).ToList();
        if (assigneeIds.Distinct().Count() != assigneeIds.Count)
            throw new InvalidOperationException("Исполнитель указан дважды");

        var users = await _db.Users.Where(u => assigneeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.OrgUnitId }).ToListAsync();
        if (users.Count != assigneeIds.Count)
            throw new InvalidOperationException("Исполнитель не найден");

        foreach (var a in req.Assignments)
        {
            if (string.IsNullOrWhiteSpace(a.Text))
                throw new InvalidOperationException("У каждого поручения должен быть текст");

            _db.SzAssignments.Add(new SzAssignment
            {
                SzDocumentId = sz.Id,
                AssigneeUserId = a.AssigneeUserId,
                AssigneeUnitId = users.First(u => u.Id == a.AssigneeUserId).OrgUnitId,
                Text = a.Text.Trim(),
                IsPrimary = a.IsPrimary,
                // Срок поручения по умолчанию равен сроку записки: исполнитель не должен
                // догадываться, к какой дате свести результат.
                DueDate = a.DueDate ?? sz.DueDate,
                CreatedByUserId = actorUserId
            });
        }

        sz.ExecutionResolution = req.Text.Trim();
        sz.ExecutionResolutionByUserId = actorUserId;
        sz.ExecutionResolutionAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Sz", sz.Id, "Resolution", actorUserId,
            new { assignments = req.Assignments.Count });

        return await ListAsync(szId);
    }

    public async Task<List<SzAssignmentResponse>> ListAsync(int szId)
    {
        var items = await BaseQuery().Where(a => a.SzDocumentId == szId)
            .OrderByDescending(a => a.IsPrimary).ThenBy(a => a.Id)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    public async Task<List<SzAssignmentResponse>> MyAsync(int currentUserId, bool includeClosed = false)
    {
        var query = BaseQuery().Where(a => a.AssigneeUserId == currentUserId);
        if (!includeClosed)
            query = query.Where(a => a.State == SzAssignmentState.Open || a.State == SzAssignmentState.Reported);

        var items = await query
            .OrderBy(a => a.DueDate ?? DateOnly.MaxValue).ThenBy(a => a.Id)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    public async Task<SzAssignmentResponse> ReportAsync(int assignmentId, string reportText, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(reportText))
            throw new InvalidOperationException("Отчёт по поручению обязателен");

        var a = await LoadAssignmentAsync(assignmentId);

        if (a.AssigneeUserId != actorUserId)
            throw new InvalidOperationException("Отчитаться по поручению может только его исполнитель");
        if (a.State is not (SzAssignmentState.Open or SzAssignmentState.Reported))
            throw new InvalidOperationException("Поручение уже закрыто");

        a.ReportText = reportText.Trim();
        a.ReportedAt = DateTime.UtcNow;
        a.State = SzAssignmentState.Reported;
        // Повторная сдача после возврата: старая причина возврата не должна висеть.
        a.ReturnReason = null;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("SzAssignment", a.Id, "Reported", actorUserId);

        return Map(await LoadAssignmentAsync(assignmentId));
    }

    public async Task<SzAssignmentResponse> AcceptAsync(int assignmentId, int actorUserId)
    {
        var a = await LoadAssignmentAsync(assignmentId);
        EnsureController(a, actorUserId);

        if (a.State != SzAssignmentState.Reported)
            throw new InvalidOperationException("Принять можно только сданное поручение");

        a.State = SzAssignmentState.Done;
        a.ClosedByUserId = actorUserId;
        a.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("SzAssignment", a.Id, "Accepted", actorUserId);

        await TryCompleteAsync(a.SzDocumentId, actorUserId);

        return Map(await LoadAssignmentAsync(assignmentId));
    }

    public async Task<SzAssignmentResponse> ReturnAsync(int assignmentId, string reason, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Укажите причину возврата");

        var a = await LoadAssignmentAsync(assignmentId);
        EnsureController(a, actorUserId);

        if (a.State != SzAssignmentState.Reported)
            throw new InvalidOperationException("Вернуть можно только сданное поручение");

        a.State = SzAssignmentState.Open;
        a.ReturnReason = reason.Trim();

        await _db.SaveChangesAsync();
        await _audit.LogAsync("SzAssignment", a.Id, "Returned", actorUserId, new { reason = a.ReturnReason });

        return Map(await LoadAssignmentAsync(assignmentId));
    }

    public async Task<SzAssignmentResponse> CancelAsync(int assignmentId, int actorUserId)
    {
        var a = await LoadAssignmentAsync(assignmentId);
        EnsureController(a, actorUserId);

        if (a.State == SzAssignmentState.Done)
            throw new InvalidOperationException("Исполненное поручение снять нельзя");

        a.State = SzAssignmentState.Cancelled;
        a.ClosedByUserId = actorUserId;
        a.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("SzAssignment", a.Id, "Cancelled", actorUserId);

        // Снятие последнего открытого поручения тоже закрывает записку.
        await TryCompleteAsync(a.SzDocumentId, actorUserId);

        return Map(await LoadAssignmentAsync(assignmentId));
    }

    public async Task ExtendDueDateAsync(int szId, DateOnly dueDate, string reason, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Укажите обоснование продления срока");

        var sz = await LoadAsync(szId);

        if (sz.Document!.StatusCode != SzStatus.OnExecution)
            throw new InvalidOperationException("Срок продлевается у записки на исполнении");
        if (sz.DueDate != null && dueDate <= sz.DueDate)
            throw new InvalidOperationException("Новый срок должен быть позже текущего");

        var previous = sz.DueDate;
        sz.DueDate = dueDate;
        sz.DueDateExtensionReason = reason.Trim();
        sz.DueDateExtensions++;

        // Поручения, которые шли к прежнему сроку, двигаются вместе с запиской:
        // иначе исполнители остаются просроченными при живом сроке записки.
        foreach (var a in await _db.SzAssignments
                     .Where(x => x.SzDocumentId == sz.Id
                                 && x.State != SzAssignmentState.Done
                                 && x.State != SzAssignmentState.Cancelled
                                 && x.DueDate == previous)
                     .ToListAsync())
        {
            a.DueDate = dueDate;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Sz", sz.Id, "DueDateExtended", actorUserId,
            new { from = previous, to = dueDate, reason = sz.DueDateExtensionReason });
    }

    public async Task CompleteAsync(int szId, string summary, int actorUserId)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new InvalidOperationException("Укажите итог исполнения");

        var sz = await LoadAsync(szId);

        if (sz.Document!.StatusCode != SzStatus.OnExecution)
            throw new InvalidOperationException("Исполненной отмечается записка на исполнении");

        var open = await _db.SzAssignments.CountAsync(a => a.SzDocumentId == sz.Id
            && a.State != SzAssignmentState.Done && a.State != SzAssignmentState.Cancelled);
        if (open > 0)
            throw new InvalidOperationException($"Осталось незакрытых поручений: {open}");

        await CloseAsync(sz, summary.Trim(), actorUserId);
        await _db.SaveChangesAsync();
    }

    // --- вспомогательное ---

    /// <summary>Закрыть записку автоматически, когда закрыто последнее поручение.</summary>
    private async Task TryCompleteAsync(int szId, int actorUserId)
    {
        var sz = await LoadAsync(szId);
        if (sz.Document!.StatusCode != SzStatus.OnExecution) return;

        var assignments = await _db.SzAssignments.Where(a => a.SzDocumentId == szId).ToListAsync();
        if (assignments.Count == 0) return;

        var open = assignments.Count(a => a.State != SzAssignmentState.Done
                                          && a.State != SzAssignmentState.Cancelled);
        if (open > 0) return;

        // Все поручения сняты — записка формально не исполнена, закрывать её должен человек.
        if (assignments.All(a => a.State == SzAssignmentState.Cancelled)) return;

        await CloseAsync(sz, "Исполнено: все поручения по записке закрыты", actorUserId);
        await _db.SaveChangesAsync();
    }

    private async Task CloseAsync(SzDocument sz, string summary, int actorUserId)
    {
        sz.ExecutionSummary = summary;
        sz.ExecutedAt = DateTime.UtcNow;

        await _documents.ChangeStatusAsync(sz.DocumentId, SzStatus.Executed, actorUserId);
        await _audit.LogAsync("Sz", sz.Id, "Executed", actorUserId, new { summary });
    }

    /// <summary>Принимает и снимает поручения автор резолюции или автор записки.</summary>
    private static void EnsureController(SzAssignment a, int actorUserId)
    {
        var allowed = a.CreatedByUserId == actorUserId
                      || a.SzDocument?.Document?.AuthorId == actorUserId;
        if (!allowed)
            throw new InvalidOperationException("Принимать отчёты может автор резолюции или автор записки");
    }

    private IQueryable<SzAssignment> BaseQuery() => _db.SzAssignments.AsNoTracking()
        .Include(a => a.AssigneeUser)
        .Include(a => a.AssigneeUnit)
        .Include(a => a.SzDocument).ThenInclude(s => s!.Document);

    private async Task<SzDocument> LoadAsync(int szId) =>
        await _db.SzDocuments.Include(x => x.Document).FirstOrDefaultAsync(x => x.Id == szId)
        ?? throw new KeyNotFoundException("Служебная записка не найдена");

    private async Task<SzAssignment> LoadAssignmentAsync(int id) =>
        await _db.SzAssignments
            .Include(a => a.AssigneeUser)
            .Include(a => a.AssigneeUnit)
            .Include(a => a.SzDocument).ThenInclude(s => s!.Document)
            .FirstOrDefaultAsync(a => a.Id == id)
        ?? throw new KeyNotFoundException("Поручение не найдено");

    private static SzAssignmentResponse Map(SzAssignment a)
    {
        var live = a.State is SzAssignmentState.Open or SzAssignmentState.Reported;
        return new SzAssignmentResponse
        {
            Id = a.Id,
            SzDocumentId = a.SzDocumentId,
            AssigneeUserId = a.AssigneeUserId,
            AssigneeName = a.AssigneeUser?.FullName,
            AssigneeUnit = a.AssigneeUnit?.TitleRu,
            Text = a.Text,
            IsPrimary = a.IsPrimary,
            DueDate = a.DueDate,
            State = a.State.ToString(),
            ReportText = a.ReportText,
            ReportedAt = a.ReportedAt,
            ClosedAt = a.ClosedAt,
            ReturnReason = a.ReturnReason,
            IsOverdue = live && a.DueDate != null && a.DueDate < Today,
            DaysLeft = a.DueDate == null ? null : a.DueDate.Value.DayNumber - Today.DayNumber,
            SzRegNumber = a.SzDocument?.Document?.RegNumber,
            SzTitle = a.SzDocument?.Document?.Title
        };
    }
}
