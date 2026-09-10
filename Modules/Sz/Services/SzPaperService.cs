using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzPaperService
{
    /// <summary>Выдать бумажный оригинал под контроль возврата (SZ-PAP).</summary>
    Task<SzOriginalResponse> HandOverAsync(int szId, SzHandoverRequest req, int actorUserId);

    /// <summary>Принять оригинал обратно.</summary>
    Task<SzOriginalResponse> ReturnAsync(int szId, int actorUserId);

    /// <summary>Состояние оригинала по записке.</summary>
    Task<SzOriginalResponse> GetOriginalAsync(int szId);

    /// <summary>Реестр невозвращённых оригиналов — по нему делопроизводство собирает бумагу.</summary>
    Task<List<SzOriginalResponse>> OutstandingAsync(bool overdueOnly = false);

    /// <summary>Печатная форма записки с листом согласования.</summary>
    Task<SzPrintFormResponse> PrintFormAsync(int szId);
}

public class SzPaperService : ISzPaperService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public SzPaperService(DelosferaDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    // Контроль бумажного оригинала — делопроизводство: право «регистрировать записки»
    // или «видеть все записки» снимает привязку к подразделению. Остальным доступна
    // своя записка и записки своего подразделения.
    private bool CanManageAll =>
        _currentUser.HasPermission(PermissionCode.RegisterSz)
        || _currentUser.HasPermission(PermissionCode.ViewAllSz);

    private async Task EnsureAccessAsync(SzDocument sz)
    {
        if (!await SzVisibility.CanAccessAsync(_db, sz, _currentUser.UserId, CanManageAll))
            throw new UnauthorizedAccessException("Нет доступа к бумажному контуру этой записки");
    }

    public async Task<SzOriginalResponse> HandOverAsync(int szId, SzHandoverRequest req, int actorUserId)
    {
        var sz = await LoadAsync(szId);
        await EnsureAccessAsync(sz);

        if (!sz.Document!.IsPaperCarrier)
            throw new InvalidOperationException("Записка ведётся электронно: бумажного оригинала нет");
        if (sz.OriginalHandedAt != null && sz.OriginalReturnedAt == null)
            throw new InvalidOperationException(
                $"Оригинал уже на руках у {sz.OriginalHolderUser?.FullName ?? "получателя"}; сначала примите возврат");
        if (!await _db.Users.AnyAsync(u => u.Id == req.HolderUserId))
            throw new KeyNotFoundException("Получатель не найден");
        if (req.DueBackOn != null && req.DueBackOn < Today)
            throw new InvalidOperationException("Дата возврата не может быть в прошлом");

        sz.OriginalHolderUserId = req.HolderUserId;
        sz.OriginalHandedAt = DateTime.UtcNow;
        sz.OriginalDueBackOn = req.DueBackOn;
        sz.OriginalLocation = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location.Trim();
        sz.OriginalHandoverCount++;
        // Новая выдача открывает новый цикл: прошлый возврат к ней не относится.
        sz.OriginalReturnedAt = null;
        sz.OriginalReturnedToUserId = null;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Sz", sz.Id, "OriginalHandedOver", actorUserId,
            new { holder = req.HolderUserId, dueBackOn = req.DueBackOn, location = sz.OriginalLocation });

        return await GetOriginalAsync(szId);
    }

    public async Task<SzOriginalResponse> ReturnAsync(int szId, int actorUserId)
    {
        var sz = await LoadAsync(szId);
        await EnsureAccessAsync(sz);

        if (sz.OriginalHandedAt == null || sz.OriginalReturnedAt != null)
            throw new InvalidOperationException("Оригинал не выдавался или уже возвращён");

        sz.OriginalReturnedAt = DateTime.UtcNow;
        sz.OriginalReturnedToUserId = actorUserId;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Sz", sz.Id, "OriginalReturned", actorUserId,
            new { holder = sz.OriginalHolderUserId, overdue = sz.OriginalDueBackOn != null && sz.OriginalDueBackOn < Today });

        return await GetOriginalAsync(szId);
    }

    public async Task<SzOriginalResponse> GetOriginalAsync(int szId)
    {
        var sz = await LoadAsync(szId);
        await EnsureAccessAsync(sz);
        return Map(sz);
    }

    public async Task<List<SzOriginalResponse>> OutstandingAsync(bool overdueOnly = false)
    {
        // Реестр невозвращённых оригиналов сводит записки всех подразделений — это
        // рабочий инструмент делопроизводства, не карточка отдельной записки.
        if (!CanManageAll)
            throw new UnauthorizedAccessException("Реестр оригиналов доступен делопроизводству");

        var query = _db.SzDocuments.AsNoTracking()
            .Include(x => x.Document)
            .Include(x => x.OriginalHolderUser)
            .Where(x => x.OriginalHandedAt != null && x.OriginalReturnedAt == null);

        if (overdueOnly)
            query = query.Where(x => x.OriginalDueBackOn != null && x.OriginalDueBackOn < Today);

        var items = await query
            .OrderBy(x => x.OriginalDueBackOn ?? DateOnly.MaxValue)
            .ToListAsync();

        return items.Select(Map).ToList();
    }

    public async Task<SzPrintFormResponse> PrintFormAsync(int szId)
    {
        var sz = await _db.SzDocuments.AsNoTracking()
            .Include(x => x.Document).ThenInclude(d => d!.Author).ThenInclude(a => a!.OrgUnit)
            .Include(x => x.Kind)
            .Include(x => x.HrKind)
            .Include(x => x.AuthorUnit)
            .Include(x => x.CorrespondentUnit)
            .Include(x => x.AddresseeUser)
            .Include(x => x.EmployeeUnit)
            .Include(x => x.TransferUnit)
            .FirstOrDefaultAsync(x => x.Id == szId)
            ?? throw new KeyNotFoundException("Служебная записка не найдена");

        await EnsureAccessAsync(sz);

        var form = new SzPrintFormResponse
        {
            RegNumber = sz.Document!.RegNumber,
            RegisteredOn = sz.RegisteredOn,
            Title = sz.Document.Title,
            Body = sz.Body,
            Kind = sz.Kind?.TitleRu,
            HrKind = sz.HrKind?.TitleRu,
            AuthorName = sz.Document.Author?.FullName,
            AuthorUnit = sz.AuthorUnit?.TitleRu ?? sz.Document.Author?.OrgUnit?.TitleRu,
            // Адресат на бумаге — тот, кому записка направлена: сначала конкретный
            // человек из «Кому», и лишь при его отсутствии — подразделение.
            CorrespondentUnit = sz.AddresseeUser is null
                ? sz.CorrespondentUnit?.TitleRu
                : sz.CorrespondentUnit is null
                    ? sz.AddresseeUser.FullName
                    : $"{sz.AddresseeUser.FullName}, {sz.CorrespondentUnit.TitleRu}",
            DueDate = sz.DueDate,
            IsPaperCarrier = sz.Document.IsPaperCarrier,
            ExecutionResolution = sz.ExecutionResolution,

            // Решение адресата — то, по чему записка исполняется. Без него бумажная
            // копия не показывает, чем дело кончилось.
            AddresseeDecision = sz.AddresseeDecision,
            AddresseeDecisionAt = sz.AddresseeDecisionAt,
            AddresseeName = sz.AddresseeUser?.FullName,
        };

        // В печать идут только заполненные реквизиты: пустые строки в бумажной форме мешают.
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) form.Fields.Add(new SzPrintFieldResponse { Label = label, Value = value });
        }

        Add("Сотрудник", sz.EmployeeName);
        Add("Филиал/СП сотрудника", sz.EmployeeUnit?.TitleRu);
        Add("Филиал/СП перевода", sz.TransferUnit?.TitleRu);
        if (sz.HasBudget is bool budget) Add("Заложено в бюджет", budget ? "Да" : "Нет");
        if (sz.Amount is decimal amount) Add("Сумма", amount.ToString("N2"));
        if (sz.TravelExpenses is bool travel) Add("Командировочные расходы", travel ? "Да" : "Нет");

        form.Approvals = await BuildApprovalSheetAsync(sz.DocumentId);

        form.Assignments = await _db.SzAssignments.AsNoTracking()
            .Include(a => a.AssigneeUser)
            .Where(a => a.SzDocumentId == sz.Id && a.State != SzAssignmentState.Cancelled)
            .OrderByDescending(a => a.IsPrimary).ThenBy(a => a.Id)
            .Select(a => new SzPrintAssignmentResponse
            {
                AssigneeName = a.AssigneeUser!.FullName,
                Text = a.Text,
                DueDate = a.DueDate,
                IsPrimary = a.IsPrimary
            })
            .ToListAsync();

        return form;
    }

    // --- вспомогательное ---

    /// <summary>
    /// Лист согласования собирается по всем маршрутам документа, а не только по текущему:
    /// на бумаге должна остаться история виз, включая прошлые круги согласования.
    /// </summary>
    private async Task<List<SzPrintApprovalResponse>> BuildApprovalSheetAsync(int documentId)
    {
        var steps = await _db.RouteSteps.AsNoTracking()
            .Include(s => s.RouteInstance)
            .Include(s => s.Participants).ThenInclude(p => p.Resolution)
            .Where(s => s.RouteInstance!.DocumentId == documentId
                        && s.RouteInstance.Status != RouteInstanceStatus.Interrupted)
            .OrderBy(s => s.RouteInstanceId).ThenBy(s => s.Order)
            .ToListAsync();

        var userIds = steps.SelectMany(s => s.Participants)
            .Where(p => p.UserId != null).Select(p => p.UserId!.Value).Distinct().ToList();

        var users = await _db.Users.AsNoTracking()
            .Include(u => u.Position)
            .Include(u => u.OrgUnit)
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u);

        // Подписи под визами — одним запросом на весь лист согласования.
        var signatureIds = steps.SelectMany(s => s.Participants)
            .Select(p => p.Resolution?.SignatureId)
            .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();

        var signatures = signatureIds.Count == 0
            ? []
            : await _db.Signatures.AsNoTracking()
                .Where(s => signatureIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

        var sheet = new List<SzPrintApprovalResponse>();
        foreach (var step in steps)
        {
            foreach (var p in step.Participants.OrderBy(p => p.Id))
            {
                if (p.State == ParticipantState.Cancelled) continue;

                users.TryGetValue(p.UserId ?? 0, out var user);
                sheet.Add(new SzPrintApprovalResponse
                {
                    StepOrder = step.Order,
                    StepKind = step.Kind.ToString(),
                    UserName = user?.FullName,
                    Position = user?.Position?.TitleRu,
                    Unit = user?.OrgUnit?.TitleRu,
                    Resolution = p.Resolution?.Type.ToString(),
                    Comment = p.Resolution?.Comment,
                    ResolvedAt = p.Resolution?.At,
                    Signature = PrintStamp(p.Resolution?.SignatureId, signatures)
                });
            }
        }

        return sheet;
    }

    /// <summary>
    /// Штамп подписи для печатной формы. Реквизиты берём из самой подписи: в бумаге
    /// должно стоять то, кем человек был в день подписания, а не сегодня.
    /// </summary>
    private static SzPrintSignatureResponse? PrintStamp(
        int? signatureId, IReadOnlyDictionary<int, Signature> signatures)
    {
        if (signatureId is not { } id || !signatures.TryGetValue(id, out var signature))
            return null;

        string? fullName = null, position = null, levelTitle = null;
        if (!string.IsNullOrWhiteSpace(signature.StampMeta))
        {
            try
            {
                var meta = JsonDocument.Parse(signature.StampMeta).RootElement;
                fullName = Text(meta, "fullName");
                position = Text(meta, "position");
                levelTitle = Text(meta, "levelTitle");
            }
            catch (JsonException)
            {
                // Реквизиты старой подписи могли быть записаны иначе — печатаем
                // подпись без них, но саму подпись не теряем.
            }
        }

        return new SzPrintSignatureResponse
        {
            LevelTitle = levelTitle ?? (signature.Level == SignatureLevel.Qualified
                ? "Квалифицированная электронная подпись"
                : "Простая электронная подпись"),
            FullName = fullName,
            Position = position,
            At = signature.At,
            Fingerprint = signature.ContentHash?[..Math.Min(12, signature.ContentHash.Length)],
            Revoked = signature.Revoked,
            RevokedReason = signature.RevokedReason,
        };

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private async Task<SzDocument> LoadAsync(int szId) =>
        await _db.SzDocuments
            .Include(x => x.Document)
            .Include(x => x.OriginalHolderUser)
            .FirstOrDefaultAsync(x => x.Id == szId)
        ?? throw new KeyNotFoundException("Служебная записка не найдена");

    private static SzOriginalResponse Map(SzDocument sz)
    {
        var isOut = sz.OriginalHandedAt != null && sz.OriginalReturnedAt == null;
        return new SzOriginalResponse
        {
            SzId = sz.Id,
            RegNumber = sz.Document?.RegNumber,
            Title = sz.Document?.Title,
            IsPaperCarrier = sz.Document?.IsPaperCarrier ?? false,
            HolderUserId = sz.OriginalHolderUserId,
            HolderName = sz.OriginalHolderUser?.FullName,
            HandedAt = sz.OriginalHandedAt,
            DueBackOn = sz.OriginalDueBackOn,
            Location = sz.OriginalLocation,
            ReturnedAt = sz.OriginalReturnedAt,
            HandoverCount = sz.OriginalHandoverCount,
            IsOut = isOut,
            IsOverdue = isOut && sz.OriginalDueBackOn != null && sz.OriginalDueBackOn < Today,
            DaysLeft = sz.OriginalDueBackOn == null ? null : sz.OriginalDueBackOn.Value.DayNumber - Today.DayNumber
        };
    }
}
