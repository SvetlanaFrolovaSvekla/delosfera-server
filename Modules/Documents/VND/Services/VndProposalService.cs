using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public class VndProposalService : IVndProposalService
{
    private static readonly HashSet<string> AllowedTargets = ["ru", "kg", "en"];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Сколько символов текста предложения попадает в уведомление/письмо — дальше
    /// многоточие, полный текст открывается по ссылке.</summary>
    private const int NotificationExcerptLength = 400;

    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _files;
    private readonly INotificationService _notifications;
    private readonly ILogger<VndProposalService> _logger;

    public VndProposalService(
        DelosferaDbContext db, IFileStorageService files, INotificationService notifications,
        ILogger<VndProposalService> logger)
    {
        _db = db;
        _files = files;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<VndProposalCreatedResponse> CreateAsync(
        int vndId, CreateVndProposalRequest request, IReadOnlyList<IFormFile> files, int currentUserId,
        CancellationToken ct = default)
    {
        var vnd = await _db.VndDocuments.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vndId, ct)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status == VndStatus.Draft)
            throw new InvalidOperationException("Предложения можно направлять только по опубликованным ВНД");

        var text = (request.Text ?? "").Trim();
        if (text.Length > VndProposalLimits.MaxTextLength)
            throw new InvalidOperationException(
                $"Текст предложения не должен превышать {VndProposalLimits.MaxTextLength} символов");

        var quotes = ParseQuotes(request.QuotesJson);
        if (text.Length == 0 && quotes.Count == 0)
            throw new InvalidOperationException("Напишите предложение или сошлитесь на текст редакции");

        if (files.Count > VndProposalLimits.MaxAttachments)
            throw new InvalidOperationException(
                $"К предложению можно приложить не больше {VndProposalLimits.MaxAttachments} файлов");
        var oversized = files.FirstOrDefault(f => f.Length > VndProposalLimits.MaxAttachmentSizeBytes);
        if (oversized != null)
            throw new InvalidOperationException(
                $"Файл «{oversized.FileName}» превышает допустимый размер ({VndProposalLimits.MaxAttachmentSizeBytes / 1024 / 1024} МБ)");

        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var recent = await _db.VndProposals
            .CountAsync(p => p.AuthorUserId == currentUserId && p.CreatedAt >= hourAgo, ct);
        if (recent >= VndProposalLimits.MaxPerHour)
            throw new InvalidOperationException("Слишком много предложений подряд. Попробуйте позже.");

        int? redactionId = null;
        string? redactionCode = null;
        if (request.RedactionId.HasValue)
        {
            var redaction = await _db.VndRedactions.AsNoTracking()
                .Where(r => r.Id == request.RedactionId.Value && r.VndId == vndId)
                .Select(r => new { r.Id, r.Code })
                .FirstOrDefaultAsync(ct);
            redactionId = redaction?.Id;
            redactionCode = redaction?.Code;
        }

        var now = DateTime.UtcNow;
        var proposal = new VndProposal
        {
            VndId = vndId,
            RedactionId = redactionId,
            AuthorUserId = currentUserId,
            Text = text,
            CreatedAt = now,
            Quotes = quotes.Select((q, i) => new VndProposalQuote
            {
                SortOrder = i,
                DocumentTarget = q.DocumentTarget,
                Text = q.Text,
                Note = q.Note,
            }).ToList(),
        };

        foreach (var file in files)
        {
            var saved = await _files.SaveAsync(file, currentUserId, ct);
            proposal.Attachments.Add(new VndProposalAttachment { FileAttachmentId = saved.Id, CreatedAt = now });
        }

        _db.VndProposals.Add(proposal);
        await _db.SaveChangesAsync(ct);

        await NotifyRecipientsAsync(proposal, vnd, redactionCode, currentUserId);

        return new VndProposalCreatedResponse { Id = proposal.Id, CreatedAt = proposal.CreatedAt };
    }

    public async Task<VndProposalPagedResponse> SearchAsync(VndProposalFilterRequest filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = _db.VndProposals.AsNoTracking().AsQueryable();

        switch ((filter.Status ?? "all").ToLowerInvariant())
        {
            case "unread":
                query = query.Where(p => p.ReadAt == null);
                break;
            case "read":
                query = query.Where(p => p.ReadAt != null);
                break;
        }

        if (filter.VndId.HasValue)
            query = query.Where(p => p.VndId == filter.VndId.Value);

        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Text, pattern)
                || EF.Functions.ILike(p.Vnd!.Code, pattern)
                || EF.Functions.ILike(p.Vnd!.TitleRu, pattern)
                || EF.Functions.ILike(p.AuthorUser!.FullName, pattern)
                || p.Quotes.Any(q => EF.Functions.ILike(q.Text, pattern)
                                     || (q.Note != null && EF.Functions.ILike(q.Note, pattern))));
        }

        var total = await query.CountAsync(ct);

        // Сначала непрочитанные, внутри - от новых к старым.
        var ids = await query
            .OrderBy(p => p.ReadAt != null)
            .ThenByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var items = await LoadResponsesAsync(ids, ct);

        return new VndProposalPagedResponse
        {
            Items = ids.Select(id => items[id]).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<VndProposalResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var items = await LoadResponsesAsync([id], ct);
        return items.TryGetValue(id, out var item)
            ? item
            : throw new KeyNotFoundException($"Предложение с id={id} не найдено");
    }

    public async Task<VndProposalCountsResponse> GetCountsAsync(CancellationToken ct = default)
    {
        var total = await _db.VndProposals.CountAsync(ct);
        var unread = await _db.VndProposals.CountAsync(p => p.ReadAt == null, ct);
        return new VndProposalCountsResponse { Total = total, Unread = unread };
    }

    public async Task<VndProposalResponse> MarkAsReadAsync(int id, int currentUserId, CancellationToken ct = default)
    {
        var proposal = await _db.VndProposals.FirstOrDefaultAsync(p => p.Id == id, ct)
                       ?? throw new KeyNotFoundException($"Предложение с id={id} не найдено");
        if (proposal.ReadAt == null)
        {
            proposal.ReadAt = DateTime.UtcNow;
            proposal.ReadByUserId = currentUserId;
            await _db.SaveChangesAsync(ct);
        }
        return await GetByIdAsync(id, ct);
    }

    public async Task<VndProposalResponse> MarkAsUnreadAsync(int id, CancellationToken ct = default)
    {
        var proposal = await _db.VndProposals.FirstOrDefaultAsync(p => p.Id == id, ct)
                       ?? throw new KeyNotFoundException($"Предложение с id={id} не найдено");
        if (proposal.ReadAt != null)
        {
            proposal.ReadAt = null;
            proposal.ReadByUserId = null;
            await _db.SaveChangesAsync(ct);
        }
        return await GetByIdAsync(id, ct);
    }

    public async Task<int> MarkAllAsReadAsync(int currentUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.VndProposals
            .Where(p => p.ReadAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ReadAt, now)
                .SetProperty(p => p.ReadByUserId, currentUserId), ct);
    }

    // ---------------------------------------------------------------------------------------

    private async Task<Dictionary<int, VndProposalResponse>> LoadResponsesAsync(
        IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<int, VndProposalResponse>();

        var rows = await _db.VndProposals.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new VndProposalResponse
            {
                Id = p.Id,
                VndId = p.VndId,
                VndCode = p.Vnd!.Code,
                VndTitle = p.Vnd.TitleRu,
                RedactionId = p.RedactionId,
                RedactionCode = p.Redaction != null ? p.Redaction.Code : null,
                Text = p.Text,
                Quotes = p.Quotes
                    .OrderBy(q => q.SortOrder)
                    .Select(q => new VndProposalQuoteResponse
                    {
                        DocumentTarget = q.DocumentTarget,
                        Text = q.Text,
                        Note = q.Note,
                    }).ToList(),
                Attachments = p.Attachments
                    .OrderBy(a => a.Id)
                    .Select(a => new VndProposalAttachmentResponse
                    {
                        FileId = a.FileAttachmentId,
                        FileName = a.FileAttachment!.OriginalFileName,
                        SizeBytes = a.FileAttachment.SizeBytes,
                    }).ToList(),
                AuthorUserId = p.AuthorUserId,
                AuthorName = p.AuthorUser!.FullName,
                AuthorPosition = p.AuthorUser.Position != null ? p.AuthorUser.Position.TitleRu : null,
                AuthorOrgUnit = p.AuthorUser.OrgUnit != null ? p.AuthorUser.OrgUnit.TitleRu : null,
                CreatedAt = p.CreatedAt,
                IsRead = p.ReadAt != null,
                ReadAt = p.ReadAt,
                ReadByName = p.ReadByUser != null ? p.ReadByUser.FullName : null,
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id);
    }

    private static List<VndProposalQuoteItem> ParseQuotes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        List<VndProposalQuoteItem>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<VndProposalQuoteItem>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Некорректный формат цитат");
        }

        var result = (parsed ?? [])
            .Select(q => new VndProposalQuoteItem
            {
                DocumentTarget = AllowedTargets.Contains((q.DocumentTarget ?? "").ToLowerInvariant())
                    ? q.DocumentTarget!.ToLowerInvariant()
                    : "ru",
                Text = Truncate((q.Text ?? "").Trim(), VndProposalLimits.MaxQuoteTextLength),
                Note = string.IsNullOrWhiteSpace(q.Note)
                    ? null
                    : Truncate(q.Note.Trim(), VndProposalLimits.MaxQuoteNoteLength),
            })
            .Where(q => q.Text.Length > 0)
            .ToList();

        if (result.Count > VndProposalLimits.MaxQuotes)
            throw new InvalidOperationException(
                $"В предложении может быть не больше {VndProposalLimits.MaxQuotes} цитат");

        return result;
    }

    /// <summary>Уведомление (в системе + письмо) всем, кто разбирает предложения по ВНД — право
    /// ManageVndProposals (главный редактор ВНД). Автору самому себе не шлём. Ошибка отправки не
    /// должна терять само предложение — только логируется.</summary>
    private async Task NotifyRecipientsAsync(
        VndProposal proposal, VndDocument vnd, string? redactionCode, int authorUserId)
    {
        try
        {
            var code = (int)PermissionCode.ManageVndProposals;
            var recipientIds = await _db.Users
                .Where(u => u.IsActive && u.Id != authorUserId
                            && u.Roles.Any(r => r.PermissionCodes.Contains(code)))
                .Select(u => u.Id)
                .ToListAsync();
            if (recipientIds.Count == 0)
            {
                _logger.LogWarning(
                    "Предложение по ВНД {ProposalId} некому доставить: нет активных пользователей с правом ManageVndProposals",
                    proposal.Id);
                return;
            }

            var author = await _db.Users.AsNoTracking()
                .Where(u => u.Id == authorUserId)
                .Select(u => new
                {
                    u.FullName,
                    Position = u.Position != null ? u.Position.TitleRu : null,
                })
                .FirstOrDefaultAsync();
            var authorLabel = author == null
                ? "Сотрудник"
                : string.IsNullOrWhiteSpace(author.Position) ? author.FullName : $"{author.FullName} ({author.Position})";

            var excerpt = proposal.Text.Length > NotificationExcerptLength
                ? proposal.Text[..NotificationExcerptLength].TrimEnd() + "…"
                : proposal.Text;
            var redactionPart = redactionCode != null ? $", редакция {redactionCode}" : "";
            var extrasRu = new List<string>();
            if (proposal.Quotes.Count > 0) extrasRu.Add($"цитат из текста: {proposal.Quotes.Count}");
            if (proposal.Attachments.Count > 0) extrasRu.Add($"файлов: {proposal.Attachments.Count}");
            var extrasEn = new List<string>();
            if (proposal.Quotes.Count > 0) extrasEn.Add($"quotes: {proposal.Quotes.Count}");
            if (proposal.Attachments.Count > 0) extrasEn.Add($"files: {proposal.Attachments.Count}");
            var extrasKg = new List<string>();
            if (proposal.Quotes.Count > 0) extrasKg.Add($"тексттен цитаталар: {proposal.Quotes.Count}");
            if (proposal.Attachments.Count > 0) extrasKg.Add($"файлдар: {proposal.Attachments.Count}");

            string Body(string intro, string textLabel, List<string> extras) =>
                intro
                + (excerpt.Length > 0 ? $"\n\n{textLabel}: «{excerpt}»" : "")
                + (extras.Count > 0 ? $"\n\n{string.Join(", ", extras)}" : "");

            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = $"Новое предложение по ВНД {vnd.Code}",
                TitleEn = $"New proposal on IRD {vnd.Code}",
                TitleKg = $"ИНД {vnd.Code} боюнча жаңы сунуш",
                BodyRu = Body(
                    $"{authorLabel} направил(а) предложение по ВНД «{vnd.TitleRu}» ({vnd.Code}{redactionPart}).",
                    "Текст предложения", extrasRu),
                BodyEn = Body(
                    $"{authorLabel} sent a proposal on the IRD \"{vnd.TitleEn ?? vnd.TitleRu}\" ({vnd.Code}{(redactionCode != null ? $", redaction {redactionCode}" : "")}).",
                    "Proposal", extrasEn),
                BodyKg = Body(
                    $"{authorLabel} «{vnd.TitleKg ?? vnd.TitleRu}» ИНД боюнча сунуш жөнөттү ({vnd.Code}{(redactionCode != null ? $", редакция {redactionCode}" : "")}).",
                    "Сунуштун тексти", extrasKg),
                Category = NotificationCategory.Vnd,
                Severity = NotificationSeverity.Info,
                EntityType = "VndProposal",
                EntityId = proposal.Id,
                Url = $"/vnd-proposals?id={proposal.Id}",
                UserIds = recipientIds,
            }, authorUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось отправить уведомление о предложении по ВНД {ProposalId}", proposal.Id);
        }
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
