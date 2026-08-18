using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Signing.Services;

namespace delosfera_server.Modules.Documents.Services;

/// <summary>Кого включить в лист. Способы складываются: люди, отделы и группы вместе.</summary>
public class AcknowledgementTargets
{
    public List<int> UserIds { get; set; } = [];

    /// <summary>Подразделения целиком. Вложенные не включаются — их выбирают отдельно.</summary>
    public List<int> OrgUnitIds { get; set; } = [];

    public List<int> UserGroupIds { get; set; } = [];
}

public class CreateSheetRequest
{
    public int DocumentId { get; set; }
    public string? Instruction { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool RequireSignature { get; set; } = true;
    public AcknowledgementTargets Targets { get; set; } = new();
}

public interface IAcknowledgementService
{
    Task<AcknowledgementSheet> CreateAsync(CreateSheetRequest request, int authorUserId, CancellationToken ct = default);

    /// <summary>Дослать лист тем, кого забыли или кто пришёл позже.</summary>
    Task<int> AddParticipantsAsync(int sheetId, AcknowledgementTargets targets, int actorUserId, CancellationToken ct = default);

    Task AcknowledgeAsync(int entryId, int userId, CancellationToken ct = default);
    Task RefuseAsync(int entryId, int userId, string reason, CancellationToken ct = default);

    /// <summary>Снять сотрудника с ознакомления: уволился, переведён, включён по ошибке.</summary>
    Task CancelAsync(int entryId, int actorUserId, string? reason, CancellationToken ct = default);

    Task CloseAsync(int sheetId, int actorUserId, CancellationToken ct = default);
}

/// <summary>
/// Ознакомление с документом (Б-19).
///
/// Приказ вступает в силу не когда подписан, а когда о нём узнали те, кого он
/// касается. Поэтому рассылки недостаточно — нужна роспись каждого, с временем и
/// с отпечатком той версии, которую человек видел.
///
/// Роспись ставится простой электронной подписью по тем же правилам, что виза на
/// маршруте: то же требование согласия с регламентом, тот же штамп. Ознакомление —
/// такое же юридически значимое действие, и делать для него отдельный, более слабый
/// механизм значило бы обесценить лист.
///
/// Отказ — законный исход, а не сбой. Сотрудник вправе не согласиться; кадровой
/// службе важно, что он документ видел, и что отказ зафиксирован с причиной.
/// </summary>
public class AcknowledgementService : IAcknowledgementService
{
    private readonly DelosferaDbContext _db;
    private readonly ISignatureService _signatures;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ILogger<AcknowledgementService> _logger;

    public AcknowledgementService(
        DelosferaDbContext db,
        ISignatureService signatures,
        INotificationService notifications,
        IAuditService audit,
        ILogger<AcknowledgementService> logger)
    {
        _db = db;
        _signatures = signatures;
        _notifications = notifications;
        _audit = audit;
        _logger = logger;
    }

    public async Task<AcknowledgementSheet> CreateAsync(
        CreateSheetRequest request, int authorUserId, CancellationToken ct = default)
    {
        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, ct)
            ?? throw new KeyNotFoundException("Документ не найден");

        var userIds = await ResolveAsync(request.Targets, ct);

        if (userIds.Count == 0)
            throw new InvalidOperationException(
                "Не выбрано ни одного сотрудника — лист ознакомления не с кем заводить");

        var sheet = new AcknowledgementSheet
        {
            DocumentId = document.Id,
            Instruction = Trim(request.Instruction),
            DueDate = request.DueDate,
            RequireSignature = request.RequireSignature,
            CreatedByUserId = authorUserId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.AcknowledgementSheets.Add(sheet);
        await _db.SaveChangesAsync(ct);

        await AddEntriesAsync(sheet, userIds, ct);

        await _audit.LogAsync("AcknowledgementSheet", sheet.Id, "Created", authorUserId, new
        {
            documentId = document.Id,
            document.Title,
            участников = userIds.Count,
            срок = request.DueDate,
        });

        _logger.LogInformation(
            "Лист ознакомления {SheetId} по документу {DocumentId}: {Count} участников",
            sheet.Id, document.Id, userIds.Count);

        return sheet;
    }

    public async Task<int> AddParticipantsAsync(
        int sheetId, AcknowledgementTargets targets, int actorUserId, CancellationToken ct = default)
    {
        var sheet = await _db.AcknowledgementSheets
            .FirstOrDefaultAsync(s => s.Id == sheetId, ct)
            ?? throw new KeyNotFoundException("Лист ознакомления не найден");

        if (sheet.ClosedAt is not null)
            throw new InvalidOperationException("Лист закрыт — досылать его некому");

        var userIds = await ResolveAsync(targets, ct);
        var added = await AddEntriesAsync(sheet, userIds, ct);

        if (added > 0)
            await _audit.LogAsync("AcknowledgementSheet", sheet.Id, "ParticipantsAdded", actorUserId,
                new {добавлено = added});

        return added;
    }

    public async Task AcknowledgeAsync(int entryId, int userId, CancellationToken ct = default)
    {
        var (entry, sheet) = await LoadOwnAsync(entryId, userId, ct);

        // Подпись ставится до отметки: если подписать не удалось — не принят
        // регламент, отозван сертификат, — ознакомление не должно считаться
        // состоявшимся, иначе в листе будет роспись, которой нет.
        if (sheet.RequireSignature)
        {
            var signature = await _signatures.SignDocumentAsync(
                sheet.DocumentId, SignatureLevel.Simple, userId);
            entry.SignatureId = signature.Id;
        }

        entry.State = AcknowledgementState.Acknowledged;
        entry.RespondedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("AcknowledgementEntry", entry.Id, "Acknowledged", userId, new
        {
            sheetId = sheet.Id,
            sheet.DocumentId,
            signatureId = entry.SignatureId,
        });
    }

    public async Task RefuseAsync(int entryId, int userId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                "У отказа должна быть причина — без неё его нечего обсуждать");

        var (entry, sheet) = await LoadOwnAsync(entryId, userId, ct);

        entry.State = AcknowledgementState.Refused;
        entry.Comment = reason.Trim();
        entry.RespondedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Отказ важнее согласия: он означает несогласие с приказом и почти всегда
        // требует ответа кадровой службы, поэтому уведомление идёт автору листа.
        var fio = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? "Сотрудник";

        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            TitleRu = "Отказ от ознакомления",
            TitleKg = "Таанышуудан баш тартуу",
            BodyRu = $"{fio} отказался ознакомиться с документом. Причина: {entry.Comment}",
            BodyKg = $"{fio} документ менен таанышуудан баш тартты. Себеби: {entry.Comment}",
            Category = NotificationCategory.Task,
            Severity = NotificationSeverity.Warning,
            EntityType = "Document",
            EntityId = sheet.DocumentId,
            UserIds = [sheet.CreatedByUserId],
        }, userId);

        await _audit.LogAsync("AcknowledgementEntry", entry.Id, "Refused", userId, new
        {
            sheetId = sheet.Id,
            причина = entry.Comment,
        });
    }

    public async Task CancelAsync(
        int entryId, int actorUserId, string? reason, CancellationToken ct = default)
    {
        var entry = await _db.AcknowledgementEntries
            .FirstOrDefaultAsync(e => e.Id == entryId, ct)
            ?? throw new KeyNotFoundException("Строка листа не найдена");

        // Снять можно только того, кто ещё не расписался: роспись не отменяют,
        // она уже состоялась.
        if (entry.State is AcknowledgementState.Acknowledged or AcknowledgementState.Refused)
            throw new InvalidOperationException(
                "Сотрудник уже ответил — снять его с ознакомления нельзя");

        entry.State = AcknowledgementState.Cancelled;
        entry.Comment = Trim(reason);
        entry.RespondedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("AcknowledgementEntry", entry.Id, "Cancelled", actorUserId,
            new {причина = entry.Comment});
    }

    public async Task CloseAsync(int sheetId, int actorUserId, CancellationToken ct = default)
    {
        var sheet = await _db.AcknowledgementSheets
            .FirstOrDefaultAsync(s => s.Id == sheetId, ct)
            ?? throw new KeyNotFoundException("Лист ознакомления не найден");

        sheet.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("AcknowledgementSheet", sheet.Id, "Closed", actorUserId, new
        {
            неответивших = await _db.AcknowledgementEntries
                .CountAsync(e => e.SheetId == sheetId && e.State == AcknowledgementState.Pending, ct),
        });
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    /// <summary>
    /// Свести людей, отделы и группы в один список сотрудников. Способы складываются,
    /// а не исключают друг друга: приказ касается и отдела целиком, и двух человек
    /// из соседнего.
    /// </summary>
    private async Task<List<int>> ResolveAsync(AcknowledgementTargets targets, CancellationToken ct)
    {
        var ids = new HashSet<int>(targets.UserIds);

        if (targets.OrgUnitIds.Count > 0)
        {
            var fromUnits = await _db.Users
                .Where(u => u.OrgUnitId != null && targets.OrgUnitIds.Contains(u.OrgUnitId.Value))
                .Select(u => u.Id)
                .ToListAsync(ct);

            ids.UnionWith(fromUnits);
        }

        if (targets.UserGroupIds.Count > 0)
        {
            var fromGroups = await _db.UserGroups
                .Where(g => targets.UserGroupIds.Contains(g.Id))
                .SelectMany(g => g.Users.Select(u => u.Id))
                .ToListAsync(ct);

            ids.UnionWith(fromGroups);
        }

        // Заблокированных не включаем: задача уйдёт тому, кто не может войти, и
        // повиснет незакрытой навсегда.
        var active = await _db.Users
            .Where(u => ids.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        return active;
    }

    private async Task<int> AddEntriesAsync(
        AcknowledgementSheet sheet, List<int> userIds, CancellationToken ct)
    {
        var already = await _db.AcknowledgementEntries
            .Where(e => e.SheetId == sheet.Id)
            .Select(e => e.UserId)
            .ToListAsync(ct);

        var fresh = userIds.Except(already).ToList();
        if (fresh.Count == 0) return 0;

        var units = await _db.Users
            .Where(u => fresh.Contains(u.Id))
            .Select(u => new {u.Id, u.OrgUnitId})
            .ToDictionaryAsync(u => u.Id, u => u.OrgUnitId, ct);

        var now = DateTime.UtcNow;

        _db.AcknowledgementEntries.AddRange(fresh.Select(id => new AcknowledgementEntry
        {
            SheetId = sheet.Id,
            UserId = id,
            OrgUnitId = units.GetValueOrDefault(id),
            State = AcknowledgementState.Pending,
            CreatedAt = now,
        }));

        await _db.SaveChangesAsync(ct);

        var title = await _db.Documents
            .Where(d => d.Id == sheet.DocumentId)
            .Select(d => d.Title)
            .FirstOrDefaultAsync(ct) ?? "документ";

        var срок = sheet.DueDate is {} due ? $" Срок: {due:dd.MM.yyyy}." : string.Empty;
        var срокKg = sheet.DueDate is {} dueKg ? $" Мөөнөтү: {dueKg:dd.MM.yyyy}." : string.Empty;

        // Одно уведомление на всех получателей: рассылка по одному дала бы столько
        // же записей, но столько же и запросов к базе.
        await _notifications.CreateAsync(new CreateNotificationRequest
        {
            TitleRu = "Ознакомление с документом",
            TitleKg = "Документ менен таанышуу",
            BodyRu = $"Требуется ознакомиться: «{title}».{срок}",
            BodyKg = $"Таанышуу талап кылынат: «{title}».{срокKg}",
            Category = NotificationCategory.Task,
            EntityType = "Document",
            EntityId = sheet.DocumentId,
            UserIds = fresh,
        }, sheet.CreatedByUserId);

        return fresh.Count;
    }

    /// <summary>
    /// Строка листа, принадлежащая этому сотруднику. Расписаться за другого нельзя —
    /// иначе лист перестаёт быть доказательством.
    /// </summary>
    private async Task<(AcknowledgementEntry Entry, AcknowledgementSheet Sheet)> LoadOwnAsync(
        int entryId, int userId, CancellationToken ct)
    {
        var entry = await _db.AcknowledgementEntries
            .Include(e => e.Sheet)
            .FirstOrDefaultAsync(e => e.Id == entryId, ct)
            ?? throw new KeyNotFoundException("Строка листа не найдена");

        if (entry.UserId != userId)
            throw new InvalidOperationException("Расписаться можно только за себя");

        if (entry.State != AcknowledgementState.Pending)
            throw new InvalidOperationException("Вы уже ответили по этому документу");

        var sheet = entry.Sheet
            ?? throw new InvalidOperationException("Лист ознакомления не найден");

        return (entry, sheet);
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
