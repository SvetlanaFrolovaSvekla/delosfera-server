using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Modules.Documents.Services;

namespace delosfera_server.Modules.Meetings.Services;

public interface IAgendaService
{
    Task<AgendaItemDto> AddItemAsync(int meetingId, AgendaItemRequest request);
    Task<AgendaItemDto> UpdateItemAsync(int itemId, AgendaItemRequest request);
    Task DeleteItemAsync(int itemId);

    /// <summary>Переставить вопрос в повестке: порядок вопросов задаёт секретарь.</summary>
    Task<List<AgendaItemDto>> ReorderAsync(int meetingId, List<int> itemIdsInOrder);

    Task<AgendaItemDto> AddGuestAsync(int itemId, AgendaGuestRequest request);
    Task<AgendaItemDto> RemoveGuestAsync(int guestId);

    Task<AgendaItemDto> AddAssignmentAsync(int itemId, AgendaAssignmentRequest request);

    /// <summary>Поправить поручение: исполнителя, текст или срок.</summary>
    Task<AgendaItemDto> UpdateAssignmentAsync(int assignmentId, AgendaAssignmentRequest request);

    Task<AgendaItemDto> RemoveAssignmentAsync(int assignmentId);

    Task<AgendaAssignmentDto> ReportAsync(int assignmentId, AgendaReportRequest request, int currentUserId);
}

/// <summary>
/// Повестка дня заседания: вопросы, приглашённые, поручения и отчёты об их исполнении.
///
/// Разделение прав здесь буквально по ТЗ: секретарь ведёт вопрос целиком, а исполнитель
/// (УРПК, УРПС, юрист филиала) меняет только статус и отчёт своего поручения — поэтому
/// отчёт вынесен в отдельную операцию, а не в общее редактирование вопроса.
/// </summary>
public class AgendaService : IAgendaService
{
    private readonly DelosferaDbContext _db;
    private readonly IMeetingAccessService _access;
    private readonly ICurrentUserService _currentUser;
    private readonly IBankClock _clock;
    private readonly IAuditService _audit;

    public AgendaService(
        DelosferaDbContext db,
        IMeetingAccessService access,
        ICurrentUserService currentUser,
        IBankClock clock,
        IAuditService audit)
    {
        _db = db;
        _access = access;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
    }

    public async Task<AgendaItemDto> AddItemAsync(int meetingId, AgendaItemRequest request)
    {
        var meeting = await _db.Meetings
            .Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == meetingId)
            ?? throw new KeyNotFoundException("Заседание не найдено");

        _access.RequireManage(meeting.Body);

        if (string.IsNullOrWhiteSpace(request.Topic))
            throw new InvalidOperationException("Укажите тему вопроса повестки");

        var item = new AgendaItem
        {
            MeetingId = meeting.Id,
            Order = meeting.Items.Count == 0 ? 1 : meeting.Items.Max(i => i.Order) + 1,
            Topic = request.Topic.Trim(),
            ProtocolNumber = string.IsNullOrWhiteSpace(request.ProtocolNumber)
                ? DefaultProtocolNumber(meeting)
                : request.ProtocolNumber.Trim(),
            ProtocolDate = request.ProtocolDate ?? meeting.Date,
            DraftResolution = request.DraftResolution?.Trim(),
            Decision = request.Decision?.Trim(),
            SpeakerUserId = request.SpeakerUserId,
            SpeakerHeadUserId = request.SpeakerHeadUserId,
            SpeakerUnitId = request.SpeakerUnitId ?? await UnitOfAsync(request.SpeakerUserId),
            DeputySecretaryUserId = request.DeputySecretaryUserId,
            ControllerUserId = request.ControllerUserId,
            DocumentsUrl = request.DocumentsUrl?.Trim(),
        };

        _db.AgendaItems.Add(item);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("AgendaItem", item.Id, "Created", _currentUser.UserId,
            new { item.MeetingId, item.Order, item.Topic });

        return await LoadDtoAsync(item.Id);
    }

    public async Task<AgendaItemDto> UpdateItemAsync(int itemId, AgendaItemRequest request)
    {
        var item = await LoadAsync(itemId);
        _access.RequireManage(item.Meeting!.Body);

        if (!string.IsNullOrWhiteSpace(request.Topic)) item.Topic = request.Topic.Trim();
        if (request.ProtocolNumber is not null) item.ProtocolNumber = request.ProtocolNumber.Trim();
        if (request.ProtocolDate is { } date) item.ProtocolDate = date;
        if (request.DraftResolution is not null) item.DraftResolution = request.DraftResolution.Trim();
        if (request.Decision is not null) item.Decision = request.Decision.Trim();
        if (request.DocumentsUrl is not null) item.DocumentsUrl = request.DocumentsUrl.Trim();

        item.SpeakerUserId = request.SpeakerUserId;
        item.SpeakerHeadUserId = request.SpeakerHeadUserId;
        item.SpeakerUnitId = request.SpeakerUnitId ?? await UnitOfAsync(request.SpeakerUserId);
        item.DeputySecretaryUserId = request.DeputySecretaryUserId;
        item.ControllerUserId = request.ControllerUserId;

        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(request.Decision))
            await _audit.LogAsync("AgendaItem", item.Id, "DecisionRecorded", _currentUser.UserId,
                new { item.ProtocolNumber });

        return await LoadDtoAsync(itemId);
    }

    /// <summary>
    /// Переставить вопросы повестки.
    ///
    /// Порядок вопроса значим: по нему заседание и идёт, и в уведомлении человек
    /// читает, каким по счёту слушают его вопрос. Раньше порядок только
    /// проставлялся при добавлении и изменить его было нечем — вопрос, внесённый
    /// последним, последним и обсуждался.
    /// </summary>
    public async Task<List<AgendaItemDto>> ReorderAsync(int meetingId, List<int> itemIdsInOrder)
    {
        var meeting = await _db.Meetings.Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == meetingId)
            ?? throw new KeyNotFoundException("Заседание не найдено");

        _access.RequireManage(meeting.Body);

        var known = meeting.Items.ToDictionary(i => i.Id);

        // Порядок задаётся целиком: перечислен должен быть каждый вопрос, иначе
        // пропущенный получил бы чужой номер.
        if (itemIdsInOrder.Count != known.Count || itemIdsInOrder.Any(id => !known.ContainsKey(id)))
            throw new InvalidOperationException("Перечислите все вопросы повестки в нужном порядке");

        for (var i = 0; i < itemIdsInOrder.Count; i++)
            known[itemIdsInOrder[i]].Order = i + 1;

        await _db.SaveChangesAsync();

        var result = new List<AgendaItemDto>();
        foreach (var id in itemIdsInOrder) result.Add(await LoadDtoAsync(id));

        return result;
    }

    public async Task DeleteItemAsync(int itemId)
    {
        var item = await LoadAsync(itemId);
        _access.RequireManage(item.Meeting!.Body);

        // Вопрос с отчётами об исполнении не стирается: снятое с повестки помечается
        // статусом «исключено», чтобы след поручения и отчёта остался.
        if (item.Assignments.Any(a => !string.IsNullOrWhiteSpace(a.Report)))
            throw new InvalidOperationException(
                "По вопросу есть отчёты об исполнении — снимите поручения статусом «исключено»");

        _db.AgendaItems.Remove(item);
        await _db.SaveChangesAsync();
    }

    public async Task<AgendaItemDto> AddGuestAsync(int itemId, AgendaGuestRequest request)
    {
        var item = await LoadAsync(itemId);
        _access.RequireManage(item.Meeting!.Body);

        if (item.Guests.Any(g => g.UserId == request.UserId))
            throw new InvalidOperationException("Этот сотрудник уже приглашён на вопрос");

        _db.AgendaGuests.Add(new AgendaGuest
        {
            AgendaItemId = itemId,
            UserId = request.UserId,
            OrgUnitId = request.OrgUnitId ?? await UnitOfAsync(request.UserId),
        });

        await _db.SaveChangesAsync();
        return await LoadDtoAsync(itemId);
    }

    public async Task<AgendaItemDto> RemoveGuestAsync(int guestId)
    {
        var guest = await _db.AgendaGuests
            .Include(g => g.AgendaItem).ThenInclude(i => i!.Meeting)
            .FirstOrDefaultAsync(g => g.Id == guestId)
            ?? throw new KeyNotFoundException("Приглашённый не найден");

        _access.RequireManage(guest.AgendaItem!.Meeting!.Body);

        var itemId = guest.AgendaItemId;
        _db.AgendaGuests.Remove(guest);
        await _db.SaveChangesAsync();

        return await LoadDtoAsync(itemId);
    }

    public async Task<AgendaItemDto> AddAssignmentAsync(int itemId, AgendaAssignmentRequest request)
    {
        var item = await LoadAsync(itemId);
        _access.RequireManage(item.Meeting!.Body);

        if (request.UserId <= 0)
            throw new InvalidOperationException("Укажите ответственного");

        _db.AgendaAssignments.Add(new AgendaAssignment
        {
            AgendaItemId = itemId,
            UserId = request.UserId,
            OrgUnitId = request.OrgUnitId ?? await UnitOfAsync(request.UserId),
            Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim(),
            DueDate = request.DueDate,
            Status = ExecutionStatus.New,
        });

        await _db.SaveChangesAsync();
        return await LoadDtoAsync(itemId);
    }

    /// <summary>
    /// Поправить поручение: исполнителя, текст, срок.
    ///
    /// Поручения расписывают после заседания и по горячим следам ошибаются —
    /// не тот исполнитель, не тот срок. Исправить можно было только удалением и
    /// заведением заново, а вместе с поручением стирался и отчёт по нему.
    ///
    /// Отчёт правка не трогает: его пишет исполнитель, а не секретарь.
    /// </summary>
    public async Task<AgendaItemDto> UpdateAssignmentAsync(int assignmentId, AgendaAssignmentRequest request)
    {
        var assignment = await _db.AgendaAssignments
            .Include(a => a.AgendaItem).ThenInclude(i => i!.Meeting)
            .FirstOrDefaultAsync(a => a.Id == assignmentId)
            ?? throw new KeyNotFoundException("Поручение не найдено");

        _access.RequireManage(assignment.AgendaItem!.Meeting!.Body);

        if (request.UserId > 0 && request.UserId != assignment.UserId)
        {
            assignment.UserId = request.UserId;

            // Подразделение следует за исполнителем, если его не задали явно:
            // иначе поручение осталось бы числиться за прежним отделом.
            assignment.OrgUnitId = request.OrgUnitId ?? await UnitOfAsync(request.UserId);
        }
        else if (request.OrgUnitId is { } unitId)
        {
            assignment.OrgUnitId = unitId;
        }

        if (request.Text is not null)
            assignment.Text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();

        if (request.DueDate is { } due) assignment.DueDate = due;

        await _db.SaveChangesAsync();
        return await LoadDtoAsync(assignment.AgendaItemId);
    }

    public async Task<AgendaItemDto> RemoveAssignmentAsync(int assignmentId)
    {
        var assignment = await _db.AgendaAssignments
            .Include(a => a.AgendaItem).ThenInclude(i => i!.Meeting)
            .FirstOrDefaultAsync(a => a.Id == assignmentId)
            ?? throw new KeyNotFoundException("Поручение не найдено");

        _access.RequireManage(assignment.AgendaItem!.Meeting!.Body);

        if (!string.IsNullOrWhiteSpace(assignment.Report))
            throw new InvalidOperationException(
                "По поручению есть отчёт об исполнении — закройте его статусом, а не удалением");

        var itemId = assignment.AgendaItemId;
        _db.AgendaAssignments.Remove(assignment);
        await _db.SaveChangesAsync();

        return await LoadDtoAsync(itemId);
    }

    public async Task<AgendaAssignmentDto> ReportAsync(
        int assignmentId, AgendaReportRequest request, int currentUserId)
    {
        var assignment = await _db.AgendaAssignments
            .Include(a => a.User)
            .Include(a => a.OrgUnit)
            .Include(a => a.AgendaItem).ThenInclude(i => i!.Meeting)
            .FirstOrDefaultAsync(a => a.Id == assignmentId)
            ?? throw new KeyNotFoundException("Поручение не найдено");

        var body = assignment.AgendaItem!.Meeting!.Body;

        // Отчёт пишет сам исполнитель либо тот, кому это право выдано ролью (УРПК, УРПС,
        // юрист филиала). Секретарь органа тоже может — он закрывает поручения по итогам.
        var isExecutor = assignment.UserId == currentUserId;
        if (!isExecutor && !_access.CanReport && !_access.CanManage(body))
            throw new UnauthorizedAccessException(
                "Отчёт об исполнении заполняет ответственный или сотрудник с правом ведения отчётности");

        if (request.Status == ExecutionStatus.Excluded && !_access.CanManage(body))
            throw new UnauthorizedAccessException(
                "Снять вопрос с повестки может только секретарь органа");

        if (!string.IsNullOrWhiteSpace(request.Report))
        {
            assignment.Report = request.Report.Trim();
            // Момент хранится в UTC: часы банка отвечают за календарные даты (сроки,
            // «сегодня»), а метки времени в базе — timestamptz и принимают только UTC.
            assignment.ReportedAt = DateTime.UtcNow;
            assignment.ReportedByUserId = currentUserId;
        }

        assignment.Status = request.Status;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("AgendaItem", assignment.AgendaItemId, "ExecutionReported", currentUserId,
            new { assignmentId = assignment.Id, status = assignment.Status });

        return MeetingMapper.ToDto(assignment, _clock.Today);
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    /// <summary>
    /// Номер протокола по маске [гггг-хх-х]: год, номер заседания и признак, который
    /// у КПА остаётся пустым. Значение подставляется как заготовка и правится вручную.
    /// </summary>
    private static string DefaultProtocolNumber(Meeting meeting) =>
        $"{meeting.Year}-{meeting.Number:D2}-";

    private async Task<int?> UnitOfAsync(int? userId) =>
        userId is null
            ? null
            : await _db.Users.Where(u => u.Id == userId).Select(u => u.OrgUnitId).FirstOrDefaultAsync();

    private async Task<AgendaItem> LoadAsync(int itemId) =>
        await _db.AgendaItems
            .Include(i => i.Meeting)
            .Include(i => i.Guests)
            .Include(i => i.Assignments)
            .FirstOrDefaultAsync(i => i.Id == itemId)
        ?? throw new KeyNotFoundException("Вопрос повестки не найден");

    private async Task<AgendaItemDto> LoadDtoAsync(int itemId)
    {
        var item = await _db.AgendaItems
            .Include(i => i.Meeting)
            .Include(i => i.Speaker)
            .Include(i => i.SpeakerHead)
            .Include(i => i.SpeakerUnit)
            .Include(i => i.DeputySecretary)
            .Include(i => i.Controller)
            .Include(i => i.Guests).ThenInclude(g => g.User)
            .Include(i => i.Guests).ThenInclude(g => g.OrgUnit)
            .Include(i => i.Assignments).ThenInclude(a => a.User)
            .Include(i => i.Assignments).ThenInclude(a => a.OrgUnit)
            .Include(i => i.Files).ThenInclude(f => f.File)
            .AsNoTracking()
            .FirstAsync(i => i.Id == itemId);

        return MeetingMapper.ToDto(item, _clock.Today, _access.CanManage(item.Meeting!.Body));
    }
}
