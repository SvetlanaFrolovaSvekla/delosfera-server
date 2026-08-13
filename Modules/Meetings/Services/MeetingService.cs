using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Common.Services.Authorization;

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

    public MeetingService(
        DelosferaDbContext db,
        IMeetingAccessService access,
        ICurrentUserService currentUser,
        IBankClock clock)
    {
        _db = db;
        _access = access;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<List<MeetingListItemDto>> ListAsync(MeetingFilterRequest filter)
    {
        var query = _db.Meetings
            .Include(m => m.Secretary)
            .Include(m => m.Items).ThenInclude(i => i.Assignments)
            .AsNoTracking()
            .AsQueryable();

        if (filter.Body is { } body) query = query.Where(m => m.Body == body);
        if (filter.Year is { } year) query = query.Where(m => m.Year == year);
        if (filter.From is { } from) query = query.Where(m => m.Date >= from);
        if (filter.To is { } to) query = query.Where(m => m.Date <= to);

        var meetings = await query
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Number)
            .ToListAsync();

        var today = _clock.Today;

        var result = meetings
            .Where(m => _access.SeesAllItems(m.Body) || HasOwnItems(m))
            .Select(m => ToListItem(m, today))
            .ToList();

        return filter.OverdueOnly
            ? result.Where(m => m.OverdueCount > 0).ToList()
            : result;
    }

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

        if (request.Number is { } number)
        {
            if (number <= 0) throw new InvalidOperationException("Номер заседания начинается с 01");
            await RequireFreeNumberAsync(meeting.Year, meeting.Body, number, meeting.Id);
            meeting.Number = number;
        }

        if (request.Form is { } form) meeting.Form = form;
        if (request.Time is { } time) meeting.Time = time;
        if (request.SecretaryUserId is { } secretary) meeting.SecretaryUserId = secretary;
        if (request.SecretaryUnitId is { } unit) meeting.SecretaryUnitId = unit;
        if (request.MaterialsUrl is not null) meeting.MaterialsUrl = request.MaterialsUrl.Trim();

        await _db.SaveChangesAsync();
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
        var max = await _db.Meetings
            .Where(m => m.Year == year && m.Body == body)
            .MaxAsync(m => (int?)m.Number) ?? 0;

        return max + 1;
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
