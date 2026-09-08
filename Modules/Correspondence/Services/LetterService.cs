using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Correspondence.DTO;
using delosfera_server.Modules.Correspondence.Models;

namespace delosfera_server.Modules.Correspondence.Services;

public interface ILetterService
{
    Task<LetterDto> RegisterAsync(LetterSaveRequest request, int currentUserId, CancellationToken ct = default);
    Task<LetterDto> UpdateAsync(int id, LetterSaveRequest request, CancellationToken ct = default);
    Task<LetterDto> GetAsync(int id, CancellationToken ct = default);
    Task<LetterListResult> SearchAsync(LetterFilterRequest filter, CancellationToken ct = default);

    Task<LetterDto> ResolveAsync(int id, ResolveLetterRequest request, int currentUserId, CancellationToken ct = default);
    Task<LetterDto> CloseAsync(int id, string? note, int currentUserId, CancellationToken ct = default);

    Task<List<LetterDto>> OverdueAsync(CancellationToken ct = default);

    /// <summary>Приложить к письму файл: скан оригинала, приложение, проект ответа.</summary>
    Task<LetterFileDto> AddFileAsync(int letterId, IFormFile file, int actorUserId, CancellationToken ct = default);

    Task<List<LetterFileDto>> FilesAsync(int letterId, CancellationToken ct = default);
}

/// <summary>
/// Книга регистрации входящей и исходящей корреспонденции.
///
/// Один механизм на все категории: письмо из НБКР, жалоба клиента и счёт от
/// поставщика различаются сроками и доступом, но не устройством. Разница между
/// ними живёт в LetterDeadlinePolicy, а не в отдельных модулях.
/// </summary>
public class LetterService : ILetterService
{
    private readonly DelosferaDbContext _db;
    private readonly Files.Services.IFileStorageService _storage;
    private readonly Common.Services.Authorization.ICurrentUserService _currentUser;
    private readonly Documents.Services.IAuditService _audit;

    public LetterService(
        DelosferaDbContext db,
        Common.Services.Authorization.ICurrentUserService currentUser,
        Files.Services.IFileStorageService storage,
        Documents.Services.IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _audit = audit;
    }

    /// <summary>
    /// Приложить файл к письму: скан оригинала, приложение, проект ответа.
    ///
    /// Смысл входящей корреспонденции — зарегистрировать пришедший документ, а
    /// приложить его было нечем: модель файла письма существовала, счётчик
    /// отдавался в карточке, но ни одной точки загрузки не было, и в реестре
    /// стояли письма без самих писем.
    /// </summary>
    public async Task<LetterFileDto> AddFileAsync(
        int letterId, IFormFile file, int actorUserId, CancellationToken ct = default)
    {
        var letter = await _db.CorrespondenceLetters.FirstOrDefaultAsync(l => l.Id == letterId, ct)
                     ?? throw new KeyNotFoundException("Письмо не найдено");

        EnsureVisible(letter);

        var stored = await _storage.SaveAsync(file, actorUserId, ct);

        var link = new LetterFile
        {
            LetterId = letterId,
            FileId = stored.Id,
            ContentHash = await ХешАsync(file, ct),
            UploadedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.LetterFiles.Add(link);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Letter", letterId, "FileAttached", actorUserId,
            new { fileId = stored.Id, fileName = stored.OriginalFileName });

        return new LetterFileDto
        {
            Id = link.Id,
            FileId = stored.Id,
            FileName = stored.OriginalFileName,
            SizeBytes = stored.SizeBytes,
            ContentHash = link.ContentHash,
            UploadedAt = link.CreatedAt,
        };
    }

    public async Task<List<LetterFileDto>> FilesAsync(int letterId, CancellationToken ct = default)
    {
        var letter = await _db.CorrespondenceLetters.FirstOrDefaultAsync(l => l.Id == letterId, ct)
                     ?? throw new KeyNotFoundException("Письмо не найдено");

        EnsureVisible(letter);

        return await _db.LetterFiles
            .AsNoTracking()
            .Where(f => f.LetterId == letterId)
            .OrderBy(f => f.Id)
            .Select(f => new LetterFileDto
            {
                Id = f.Id,
                FileId = f.FileId,
                FileName = f.File!.OriginalFileName,
                SizeBytes = f.File.SizeBytes,
                ContentHash = f.ContentHash,
                UploadedAt = f.CreatedAt,
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Хеш содержимого на момент загрузки.
    ///
    /// Скан бумажного документа признаётся доказательством, когда известно, кто
    /// его загрузил и что файл с тех пор не менялся: автора пишет журнал,
    /// неизменность — этот хеш.
    /// </summary>
    private static async Task<string> ХешАsync(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);

        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Отсекает запросы по счетам от тех, кому они не положены.
    ///
    /// Банковская тайна — это узкий круг допущенных, и он не совпадает с кругом
    /// читающих обычную переписку. Ограничение стоит в службе, а не в контроллере:
    /// эндпоинтов, отдающих письма, несколько, и забыть проверку в одном из них
    /// проще, чем кажется.
    ///
    /// Исполнитель и тот, кто вынес резолюцию, видят своё письмо и без права —
    /// иначе поручение невозможно исполнить.
    /// </summary>
    private IQueryable<CorrespondenceLetter> Visible(IQueryable<CorrespondenceLetter> query)
    {
        if (_currentUser.HasPermission(Users.Models.PermissionCode.ViewBankSecrecyInquiries))
            return query;

        var userId = _currentUser.UserId;

        return query.Where(l =>
            l.Category != LetterCategory.BankSecrecyInquiry
            || l.ResponsibleUserId == userId
            || l.ResolutionByUserId == userId
            || l.CreatedByUserId == userId);
    }

    /// <summary>
    /// То же правило, что и в Visible, но для одного письма: файлы запроса по
    /// счетам не должен видеть тот, кому не положено само письмо.
    /// </summary>
    private void EnsureVisible(CorrespondenceLetter letter)
    {
        if (_currentUser.HasPermission(Users.Models.PermissionCode.ViewBankSecrecyInquiries)) return;
        if (letter.Category != LetterCategory.BankSecrecyInquiry) return;

        var userId = _currentUser.UserId;

        if (letter.ResponsibleUserId == userId
            || letter.ResolutionByUserId == userId
            || letter.CreatedByUserId == userId) return;

        throw new UnauthorizedAccessException("Запросы по счетам доступны ограниченному кругу");
    }

    public async Task<LetterDto> RegisterAsync(
        LetterSaveRequest request, int currentUserId, CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error is not null) throw new InvalidOperationException(error);

        var registeredOn = request.RegisteredOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var letter = new CorrespondenceLetter
        {
            Direction = request.Direction,
            Category = request.Category,
            Year = registeredOn.Year,
            RegisteredOn = registeredOn,
            RegisteredByUserId = currentUserId,
            CorrespondentId = request.CorrespondentId,
            TheirNumber = Trim(request.TheirNumber),
            TheirDate = request.TheirDate,
            Subject = request.Subject.Trim(),
            Summary = Trim(request.Summary),
            DeliveryMethod = request.DeliveryMethod,
            SheetCount = request.SheetCount,
            Enclosures = Trim(request.Enclosures),
            ResponsibleUserId = request.ResponsibleUserId,
            ResponsibleUnitId = request.ResponsibleUnitId,
            InReplyToId = request.InReplyToId,
            SourceSzId = request.SourceSzId,
            NomenclatureCaseId = request.NomenclatureCaseId,
            CreatedByUserId = currentUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Срок: заданный вручную главнее умолчания — в предписании регулятора срок
        // стоит в самом документе.
        letter.DueDate = request.DueDate ?? LetterDeadlinePolicy.DefaultDueDate(request.Category, registeredOn);

        if (letter.DueDate is {} due)
        {
            var deadlineError = LetterDeadlinePolicy.ValidateDueDate(request.Category, registeredOn, due);
            if (deadlineError is not null) throw new InvalidOperationException(deadlineError);
        }

        letter.IsControlled = request.IsControlled ?? LetterDeadlinePolicy.ControlledByDefault(request.Category);

        letter.Status = request.Direction == LetterDirection.Outgoing && request.AsDraft
            ? LetterStatus.Draft
            : LetterStatus.Registered;

        if (letter.Status != LetterStatus.Draft)
            letter.RegNumber = await NextNumberAsync(letter.Direction, letter.Year, ct);

        await CheckReplyAsync(letter, ct);

        _db.CorrespondenceLetters.Add(letter);
        await _db.SaveChangesAsync(ct);

        if (letter.Status == LetterStatus.Draft)
            await _audit.LogAsync("Letter", letter.Id, "Created", currentUserId,
                new { direction = letter.Direction.ToString() });
        else
            await _audit.LogAsync("Letter", letter.Id, "Registered", currentUserId,
                new { regNumber = letter.RegNumber, direction = letter.Direction.ToString() });

        // Ответ закрывает срок входящего: письмо отвечено, и видно каким.
        if (letter.Direction == LetterDirection.Outgoing && letter.InReplyToId is {} parentId)
            await MarkAnsweredAsync(parentId, currentUserId, ct);

        return await GetAsync(letter.Id, ct);
    }

    public async Task<LetterDto> UpdateAsync(int id, LetterSaveRequest request, CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error is not null) throw new InvalidOperationException(error);

        var letter = await Load(id, ct);

        if (letter.Status is LetterStatus.Answered or LetterStatus.Closed or LetterStatus.Sent)
            throw new InvalidOperationException(
                "Письмо закрыто — изменить его реквизиты нельзя.");

        letter.Category = request.Category;
        letter.CorrespondentId = request.CorrespondentId;
        letter.TheirNumber = Trim(request.TheirNumber);
        letter.TheirDate = request.TheirDate;
        letter.Subject = request.Subject.Trim();
        letter.Summary = Trim(request.Summary);
        letter.DeliveryMethod = request.DeliveryMethod;
        letter.SheetCount = request.SheetCount;
        letter.Enclosures = Trim(request.Enclosures);
        letter.ResponsibleUserId = request.ResponsibleUserId;
        letter.ResponsibleUnitId = request.ResponsibleUnitId;
        letter.NomenclatureCaseId = request.NomenclatureCaseId;
        letter.UpdatedAt = DateTime.UtcNow;

        if (request.DueDate is {} due)
        {
            var registeredOn = letter.RegisteredOn ?? DateOnly.FromDateTime(letter.CreatedAt);
            var deadlineError = LetterDeadlinePolicy.ValidateDueDate(request.Category, registeredOn, due);
            if (deadlineError is not null) throw new InvalidOperationException(deadlineError);
            letter.DueDate = due;
        }

        if (request.IsControlled is {} controlled)
            letter.IsControlled = controlled;

        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Резолюция руководителя: кому и что делать, к какому сроку.</summary>
    public async Task<LetterDto> ResolveAsync(
        int id, ResolveLetterRequest request, int currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Resolution))
            throw new InvalidOperationException("Резолюция не может быть пустой.");

        var letter = await Load(id, ct);

        letter.Resolution = request.Resolution.Trim();
        letter.ResolutionAt = DateTime.UtcNow;
        letter.ResolutionByUserId = currentUserId;

        if (request.ResponsibleUserId is {} responsible)
            letter.ResponsibleUserId = responsible;

        if (request.ResponsibleUnitId is {} unit)
            letter.ResponsibleUnitId = unit;

        if (request.DueDate is {} due)
        {
            var registeredOn = letter.RegisteredOn ?? DateOnly.FromDateTime(letter.CreatedAt);
            var deadlineError = LetterDeadlinePolicy.ValidateDueDate(letter.Category, registeredOn, due);
            if (deadlineError is not null) throw new InvalidOperationException(deadlineError);
            letter.DueDate = due;
            letter.IsControlled = true;
        }

        letter.Status = LetterStatus.OnExecution;
        letter.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Letter", letter.Id, "Resolved", currentUserId,
            new { responsibleUserId = letter.ResponsibleUserId, dueDate = letter.DueDate });

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Закрыть письмо без ответа. Требует пояснения: письмо со сроком, закрытое
    /// молча, неотличимо от забытого.
    /// </summary>
    public async Task<LetterDto> CloseAsync(
        int id, string? note, int currentUserId, CancellationToken ct = default)
    {
        var letter = await Load(id, ct);

        if (letter.IsControlled && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException(
                "Письмо на контроле — укажите, чем закончилось рассмотрение.");

        letter.Status = LetterStatus.Closed;
        letter.ExecutionNote = Trim(note);
        letter.ExecutedAt = DateTime.UtcNow;
        letter.ExecutedByUserId = currentUserId;
        letter.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("Letter", letter.Id, "Closed", currentUserId,
            new { regNumber = letter.RegNumber });

        return await GetAsync(id, ct);
    }

    public async Task<LetterDto> GetAsync(int id, CancellationToken ct = default) =>
        await Project(Visible(_db.CorrespondenceLetters.AsNoTracking()).Where(l => l.Id == id))
            .FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Письмо не найдено.");

    public async Task<LetterListResult> SearchAsync(
        LetterFilterRequest filter, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = Visible(_db.CorrespondenceLetters.AsNoTracking());

        if (filter.Direction is {} direction)
            query = query.Where(l => l.Direction == direction);

        if (filter.Categories is {Count: > 0})
            query = query.Where(l => filter.Categories.Contains(l.Category));

        if (filter.Statuses is {Count: > 0})
            query = query.Where(l => filter.Statuses.Contains(l.Status));

        if (filter.CorrespondentId is {} correspondent)
            query = query.Where(l => l.CorrespondentId == correspondent);

        if (filter.ResponsibleUserId is {} responsible)
            query = query.Where(l => l.ResponsibleUserId == responsible);

        if (filter.UnitId is {} unit)
            query = query.Where(l => l.ResponsibleUnitId == unit);

        if (filter.From is {} from)
            query = query.Where(l => l.RegisteredOn >= from);

        if (filter.To is {} to)
            query = query.Where(l => l.RegisteredOn <= to);

        if (filter.OnlyControlled == true)
            query = query.Where(l => l.IsControlled);

        if (filter.OnlyOverdue == true)
        {
            query = query.Where(l =>
                l.DueDate != null
                && l.DueDate < today
                && l.Status != LetterStatus.Answered
                && l.Status != LetterStatus.Closed
                && l.Status != LetterStatus.Sent);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim();
            query = query.Where(l =>
                EF.Functions.ILike(l.Subject, $"%{text}%")
                || (l.Summary != null && EF.Functions.ILike(l.Summary, $"%{text}%"))
                || (l.RegNumber != null && EF.Functions.ILike(l.RegNumber, $"%{text}%"))
                || (l.TheirNumber != null && EF.Functions.ILike(l.TheirNumber, $"%{text}%")));
        }

        var total = await query.CountAsync(ct);

        var page = Math.Max(filter.Page, 1);
        var size = Math.Clamp(filter.PageSize, 1, 200);

        var items = await Project(query
                .OrderByDescending(l => l.RegisteredOn).ThenByDescending(l => l.Id)
                .Skip((page - 1) * size)
                .Take(size))
            .ToListAsync(ct);

        return new LetterListResult {Total = total, Page = page, PageSize = size, Items = items};
    }

    /// <summary>Что просрочено — главный вопрос к книге регистрации.</summary>
    public async Task<List<LetterDto>> OverdueAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await Project(Visible(_db.CorrespondenceLetters.AsNoTracking())
                .Where(l => l.DueDate != null
                            && l.DueDate < today
                            && l.Status != LetterStatus.Answered
                            && l.Status != LetterStatus.Closed
                            && l.Status != LetterStatus.Sent)
                .OrderBy(l => l.DueDate))
            .ToListAsync(ct);
    }

    // ── внутреннее ──────────────────────────────────────────────

    private async Task MarkAnsweredAsync(int letterId, int currentUserId, CancellationToken ct)
    {
        var parent = await _db.CorrespondenceLetters.FirstOrDefaultAsync(l => l.Id == letterId, ct);
        if (parent is null) return;

        parent.Status = LetterStatus.Answered;
        parent.ExecutedAt = DateTime.UtcNow;
        parent.ExecutedByUserId = currentUserId;
        parent.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private async Task CheckReplyAsync(CorrespondenceLetter letter, CancellationToken ct)
    {
        if (letter.InReplyToId is null) return;

        var parent = await _db.CorrespondenceLetters
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == letter.InReplyToId, ct)
            ?? throw new InvalidOperationException("Письмо, на которое отвечаем, не найдено.");

        // Отвечают исходящим на входящее. Входящее «в ответ на входящее» означает
        // либо ошибку регистрации, либо переписку, которую надо связывать иначе.
        if (letter.Direction == LetterDirection.Incoming && parent.Direction == LetterDirection.Incoming)
            throw new InvalidOperationException(
                "Входящее письмо не может быть ответом на входящее.");
    }

    private static IQueryable<LetterDto> Project(IQueryable<CorrespondenceLetter> query)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return query.Select(l => new LetterDto
        {
            Id = l.Id,
            Direction = l.Direction.ToString(),
            Category = l.Category.ToString(),
            RegNumber = l.RegNumber,
            RegisteredOn = l.RegisteredOn,
            CorrespondentId = l.CorrespondentId,
            CorrespondentTitle = l.Correspondent == null ? null : l.Correspondent.Title,
            CorrespondentKind = l.Correspondent == null ? null : l.Correspondent.Kind.ToString(),
            TheirNumber = l.TheirNumber,
            TheirDate = l.TheirDate,
            Subject = l.Subject,
            Summary = l.Summary,
            DeliveryMethod = l.DeliveryMethod.ToString(),
            SheetCount = l.SheetCount,
            Enclosures = l.Enclosures,
            Status = l.Status.ToString(),
            Resolution = l.Resolution,
            ResolutionAt = l.ResolutionAt,
            ResolutionBy = l.ResolutionByUser == null ? null : l.ResolutionByUser.FullName,
            ResponsibleUserId = l.ResponsibleUserId,
            ResponsibleName = l.ResponsibleUser == null ? null : l.ResponsibleUser.FullName,
            ResponsibleUnit = l.ResponsibleUnit == null ? null : l.ResponsibleUnit.TitleRu,
            DueDate = l.DueDate,
            IsControlled = l.IsControlled,
            IsOverdue = l.DueDate != null
                        && l.DueDate < today
                        && l.Status != LetterStatus.Answered
                        && l.Status != LetterStatus.Closed
                        && l.Status != LetterStatus.Sent,
            DaysLeft = l.DueDate == null ? null : l.DueDate.Value.DayNumber - today.DayNumber,
            ExecutionNote = l.ExecutionNote,
            ExecutedAt = l.ExecutedAt,
            InReplyToId = l.InReplyToId,
            InReplyToNumber = l.InReplyTo == null ? null : l.InReplyTo.RegNumber,
            ReplyCount = l.Replies.Count,
            SourceSzId = l.SourceSzId,
            FileCount = l.Files.Count,
        });
    }

    /// <summary>
    /// Загружает письмо для изменения — через тот же фильтр видимости, что и чтение.
    /// Иначе недопущенный к банковской тайне не увидел бы письмо в реестре, но смог
    /// бы вынести по нему резолюцию, зная идентификатор.
    ///
    /// «Не найдено», а не «нет доступа»: сам факт существования запроса по счетам
    /// конкретного клиента — уже сведения, которых у недопущенного быть не должно.
    /// </summary>
    private async Task<CorrespondenceLetter> Load(int id, CancellationToken ct) =>
        await Visible(_db.CorrespondenceLetters).FirstOrDefaultAsync(l => l.Id == id, ct)
        ?? throw new KeyNotFoundException("Письмо не найдено.");

    private static string? Validate(LetterSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            return "Укажите тему письма.";

        if (request.CorrespondentId <= 0)
            return "Выберите корреспондента.";

        if (request.SheetCount is {} sheets && sheets < 0)
            return "Число листов не может быть отрицательным.";

        return null;
    }

    /// <summary>
    /// Следующий номер в книге. Книги входящих и исходящих раздельные, нумерация
    /// в каждой своя и начинается заново с нового года — как на бумаге.
    /// </summary>
    private async Task<string> NextNumberAsync(LetterDirection direction, int year, CancellationToken ct)
    {
        var prefix = direction == LetterDirection.Incoming ? "вх" : "исх";

        var used = await _db.CorrespondenceLetters
            .Where(l => l.Direction == direction && l.Year == year && l.RegNumber != null)
            .Select(l => l.RegNumber!)
            .ToListAsync(ct);

        var max = used
            .Select(n =>
            {
                var digits = new string(n.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
                return int.TryParse(digits, out var v) ? v : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}-{max + 1}/{year}";
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
