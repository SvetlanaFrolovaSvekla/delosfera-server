using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Documents.Models;

namespace delosfera_server.Modules.Meetings.Services;

public interface IMeetingService
{
    Task<List<MeetingListItemDto>> ListAsync(MeetingFilterRequest filter);
    Task<MeetingDto> GetAsync(int id);
    Task<MeetingDto> CreateAsync(MeetingCreateRequest request, int currentUserId);
    Task<MeetingDto> UpdateAsync(int id, MeetingUpdateRequest request);
    Task DeleteAsync(int id);
}

/// <summary>
/// Журнал заседаний Правления, КПА и комитетов.
///
/// Заседание нумеруется в пределах года и органа, а не сквозным счётчиком: секретарь
/// каждого органа ведёт свою нумерацию, и с 1 января она начинается заново с «01».
/// Номер редактируемый — часть заседаний заводится задним числом, и порядок
/// восстанавливают вручную; уникальность при этом проверяется.
/// </summary>
public class MeetingService : IMeetingService
{
    private readonly DelosferaDbContext _db;
    private readonly IMeetingAccessService _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IBankClock _clock;
    private readonly IAuditService _audit;
    private readonly INumeratorService _numerator;

    public MeetingService(
        DelosferaDbContext db,
        IMeetingAccessService access,
        ICurrentUserService currentUser,
        IBankClock clock,
        IAuditService audit,
        INumeratorService numerator)
    {
        _db = db;
        _access = access;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _numerator = numerator;
    }

    /// <summary>
    /// Журнал заседаний.
    ///
    /// Отбор по доступу и подсчёты идут в базе, а не в памяти. Раньше журнал
    /// вычитывал все заседания со всеми вопросами и поручениями, а отбирал уже
    /// после: на двухстах заседаниях это десять тысяч строк ради списка из
    /// двадцати. Заодно это чинило бы себя молча — при пустом журнале разницы
    /// не видно.
    ///
    /// Приглашённые теперь тоже учитываются. Прежняя проверка читала
    /// <c>i.Guests</c>, которого не было в загрузке, — ленивой загрузки в
    /// проекте нет, коллекция всегда оказывалась пустой, и сотрудник, указанный
    /// в вопросе только приглашённым, заседания в журнале не видел.
    /// </summary>
    public async Task<List<MeetingListItemDto>> ListAsync(MeetingFilterRequest filter)
    {
        var query = _db.Meetings.AsNoTracking().AsQueryable();

        if (filter.Body is { } body) query = query.Where(m => m.Body == body);
        if (filter.Year is { } year) query = query.Where(m => m.Year == year);
        if (filter.From is { } from) query = query.Where(m => m.Date >= from);
        if (filter.To is { } to) query = query.Where(m => m.Date <= to);

        // Органы, повестку которых человек видит целиком: секретарь и члены
        // органа. Право проверяется по каждому органу, а их три — считаем
        // список заранее и передаём в запрос значением.
        var openBodies = Enum.GetValues<MeetingBody>()
            .Where(b => _access.SeesAllItems(b))
            .ToArray();

        var me = _currentUser.UserId;

        query = query.Where(m =>
            openBodies.Contains(m.Body)
            || m.Items.Any(i =>
                i.SpeakerUserId == me
                || i.SpeakerHeadUserId == me
                || i.DeputySecretaryUserId == me
                || i.ControllerUserId == me
                || i.Guests.Any(g => g.UserId == me)
                || i.Assignments.Any(a => a.UserId == me)));

        var today = _clock.Today;

        // Просрочка считается подзапросом здесь же: иначе пришлось бы тянуть
        // поручения целиком ради одного числа в строке.
        var projected = query
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Number)
            .Select(m => new
            {
                m.Id, m.Body, m.Number, m.Year, m.Form, m.Date, m.Time, m.NotifiedAt,
                SecretaryName = m.Secretary == null ? null : m.Secretary.FullName,
                ItemCount = m.Items.Count,
                AssignmentCount = m.Items.Sum(i => i.Assignments.Count),
                OverdueCount = m.Items
                    .SelectMany(i => i.Assignments)
                    .Count(a => a.DueDate != null
                                && a.DueDate < today
                                && OpenStatuses.Contains(a.Status)),
            });

        if (filter.OverdueOnly)
            projected = projected.Where(x => x.OverdueCount > 0);

        var rows = await projected.ToListAsync();

        return rows.Select(x => new MeetingListItemDto
        {
            Id = x.Id,
            Body = x.Body,
            BodyTitle = MeetingTitles.Body(x.Body),
            Number = x.Number,
            Year = x.Year,
            Form = x.Form,
            FormTitle = MeetingTitles.Form(x.Form),
            Date = x.Date,
            Time = x.Time,
            SecretaryName = x.SecretaryName,
            ItemCount = x.ItemCount,
            AssignmentCount = x.AssignmentCount,
            OverdueCount = x.OverdueCount,
            NotifiedAt = x.NotifiedAt,
        }).ToList();
    }

    /// <summary>
    /// Состояния, при которых поручение считается открытым. Список значением,
    /// а не вызовом метода: внутри запроса к базе метод не переводится в SQL.
    /// </summary>
    private static readonly ExecutionStatus[] OpenStatuses =
        Enum.GetValues<ExecutionStatus>().Where(MeetingTitles.IsOpen).ToArray();

    public async Task<MeetingDto> GetAsync(int id)
    {
        var meeting = await LoadAsync(id);
        var visible = await _access.VisibleItemIdsAsync(id);
        var today = _clock.Today;

        var dto = (MeetingDto)ToDto(meeting, today, new MeetingDto());

        dto.SecretaryUserId = meeting.SecretaryUserId;
        dto.SecretaryUnitId = meeting.SecretaryUnitId;
        dto.SecretaryUnitTitle = meeting.SecretaryUnit?.TitleRu;
        dto.MaterialsUrl = meeting.MaterialsUrl;
        dto.CanEdit = _access.CanManage(meeting.Body);
        dto.CanReport = _access.CanReport;

        dto.Items = meeting.Items
            .Where(i => visible.Contains(i.Id))
            .OrderBy(i => i.Order)
            .Select(i => MeetingMapper.ToDto(i, today, dto.CanEdit))
            .ToList();

        return dto;
    }

    public async Task<MeetingDto> CreateAsync(MeetingCreateRequest request, int currentUserId)
    {
        _access.RequireManage(request.Body);

        if (request.Date == default)
            throw new InvalidOperationException("Укажите дату заседания");

        var year = request.Date.Year;
        var number = request.Number ?? await NextNumberAsync(year, request.Body);

        if (number <= 0)
            throw new InvalidOperationException("Номер заседания начинается с 01");

        await RequireFreeNumberAsync(year, request.Body, number, null);

        var secretaryId = request.SecretaryUserId ?? currentUserId;

        var meeting = new Meeting
        {
            Body = request.Body,
            Number = number,
            Year = year,
            Form = request.Form,
            SecretaryUserId = secretaryId,
            // Управление секретаря не спрашиваем дважды: если не указано, берём его подразделение.
            SecretaryUnitId = request.SecretaryUnitId ?? await UnitOfAsync(secretaryId),
            Date = request.Date,
            Time = request.Time,
            MaterialsUrl = request.MaterialsUrl?.Trim(),
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Meeting", meeting.Id, "Created", currentUserId,
            new { meeting.Body, meeting.Number, meeting.Year });

        return await GetAsync(meeting.Id);
    }

    public async Task<MeetingDto> UpdateAsync(int id, MeetingUpdateRequest request)
    {
        var meeting = await LoadAsync(id);
        _access.RequireManage(meeting.Body);

        if (request.Date is { } date)
        {
            meeting.Date = date;

            // Год заседания — основа счётчика: при переносе на другой год номер должен
            // остаться свободным и там, иначе нумерация разъедется.
            if (date.Year != meeting.Year)
            {
                await RequireFreeNumberAsync(date.Year, meeting.Body, meeting.Number, meeting.Id);
                meeting.Year = date.Year;
            }
        }

        var renumbered = false;
        if (request.Number is { } number)
        {
            if (number <= 0) throw new InvalidOperationException("Номер заседания начинается с 01");
            await RequireFreeNumberAsync(meeting.Year, meeting.Body, number, meeting.Id);
            meeting.Number = number;
            renumbered = true;
        }

        if (request.Form is { } form) meeting.Form = form;
        if (request.Time is { } time) meeting.Time = time;
        if (request.SecretaryUserId is { } secretary) meeting.SecretaryUserId = secretary;
        if (request.SecretaryUnitId is { } unit) meeting.SecretaryUnitId = unit;
        if (request.MaterialsUrl is not null) meeting.MaterialsUrl = request.MaterialsUrl.Trim();

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Meeting", meeting.Id, "Updated", _currentUser.UserId);
        if (renumbered)
            await _audit.LogAsync("Meeting", meeting.Id, "Numbered", _currentUser.UserId,
                new { meeting.Number, meeting.Year });

        return await GetAsync(id);
    }

    public async Task DeleteAsync(int id)
    {
        var meeting = await LoadAsync(id);
        _access.RequireManage(meeting.Body);

        // Заседание с рассмотренными вопросами не удаляют: протоколы и поручения по ним
        // живут дольше самой записи. Ошибочную повестку снимают статусом «исключено».
        if (meeting.Items.Count > 0)
            throw new InvalidOperationException(
                "Заседание с вопросами повестки не удаляется — снимите вопросы статусом «исключено»");

        _db.Meetings.Remove(meeting);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Meeting", id, "Deleted", _currentUser.UserId,
            new { meeting.Body, meeting.Number, meeting.Year });
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private async Task<Meeting> LoadAsync(int id) =>
        await _db.Meetings
            .Include(m => m.Secretary)
            .Include(m => m.SecretaryUnit)
            .Include(m => m.Items).ThenInclude(i => i.Speaker)
            .Include(m => m.Items).ThenInclude(i => i.SpeakerHead)
            .Include(m => m.Items).ThenInclude(i => i.SpeakerUnit)
            .Include(m => m.Items).ThenInclude(i => i.DeputySecretary)
            .Include(m => m.Items).ThenInclude(i => i.Controller)
            .Include(m => m.Items).ThenInclude(i => i.Guests).ThenInclude(g => g.User)
            .Include(m => m.Items).ThenInclude(i => i.Guests).ThenInclude(g => g.OrgUnit)
            .Include(m => m.Items).ThenInclude(i => i.Assignments).ThenInclude(a => a.User)
            .Include(m => m.Items).ThenInclude(i => i.Assignments).ThenInclude(a => a.OrgUnit)
            .Include(m => m.Items).ThenInclude(i => i.Files).ThenInclude(f => f.File)
            .FirstOrDefaultAsync(m => m.Id == id)
        ?? throw new KeyNotFoundException("Заседание не найдено");

    private async Task<int> NextNumberAsync(int year, MeetingBody body)
    {
        var next = await _numerator.NextAsync(DocumentType.Meeting, body.ToString(), year.ToString(), "{seq}");
        return int.Parse(next);
    }

    private async Task RequireFreeNumberAsync(int year, MeetingBody body, int number, int? exceptId)
    {
        var taken = await _db.Meetings.AnyAsync(m =>
            m.Year == year && m.Body == body && m.Number == number &&
            (exceptId == null || m.Id != exceptId));

        if (taken)
            throw new InvalidOperationException(
                $"Заседание № {number:D2} за {year} год у органа «{MeetingTitles.Body(body)}» уже заведено");
    }

    private async Task<int?> UnitOfAsync(int userId) =>
        await _db.Users.Where(u => u.Id == userId).Select(u => u.OrgUnitId).FirstOrDefaultAsync();

    /// <summary>
    /// Есть ли у текущего пользователя хотя бы один доступный вопрос этого заседания.
    ///
    /// Заседание, где сотрудника не указали ни в одном вопросе, не показывается ему
    /// вовсе: иначе по журналу видно и сам факт заседания, и его повестку по числу строк.
    /// </summary>
    private bool HasOwnItems(Meeting meeting)
    {
        var me = _currentUser.UserId;

        return meeting.Items.Any(i =>
            i.SpeakerUserId == me ||
            i.SpeakerHeadUserId == me ||
            i.DeputySecretaryUserId == me ||
            i.ControllerUserId == me ||
            i.Guests.Any(g => g.UserId == me) ||
            i.Assignments.Any(a => a.UserId == me));
    }

    private MeetingListItemDto ToListItem(Meeting meeting, DateOnly today) =>
        ToDto(meeting, today, new MeetingListItemDto());

    private MeetingListItemDto ToDto(Meeting meeting, DateOnly today, MeetingListItemDto dto)
    {
        dto.Id = meeting.Id;
        dto.Body = meeting.Body;
        dto.BodyTitle = MeetingTitles.Body(meeting.Body);
        dto.Number = meeting.Number;
        dto.Year = meeting.Year;
        dto.Form = meeting.Form;
        dto.FormTitle = MeetingTitles.Form(meeting.Form);
        dto.Date = meeting.Date;
        dto.Time = meeting.Time;
        dto.SecretaryName = meeting.Secretary?.FullName;
        dto.ItemCount = meeting.Items.Count;
        dto.AssignmentCount = meeting.Items.Sum(i => i.Assignments.Count);
        dto.OverdueCount = meeting.Items
            .SelectMany(i => i.Assignments)
            .Count(a => a.DueDate is { } due && due < today && MeetingTitles.IsOpen(a.Status));
        dto.NotifiedAt = meeting.NotifiedAt;
        return dto;
    }
}
