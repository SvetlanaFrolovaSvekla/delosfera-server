using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Meetings.Services;

public interface IMeetingAccessService
{
    /// <summary>Может ли текущий пользователь вести заседания этого органа (роль секретаря).</summary>
    bool CanManage(MeetingBody body);

    /// <summary>Может ли текущий пользователь заполнять отчёты об исполнении.</summary>
    bool CanReport { get; }

    /// <summary>Видит ли текущий пользователь повестку целиком — секретарь или член органа.</summary>
    bool SeesAllItems(MeetingBody body);

    /// <summary>Требует права секретаря по органу; иначе — отказ.</summary>
    void RequireManage(MeetingBody body);

    /// <summary>Вопросы повестки заседания, доступные текущему пользователю на чтение.</summary>
    Task<HashSet<int>> VisibleItemIdsAsync(int meetingId, CancellationToken ct = default);
}

/// <summary>
/// Правила доступа к журналу заседаний.
///
/// Ключевое ограничение ТЗ: вопрос повестки читают только те, кто в нём указан —
/// докладчик, его руководитель, приглашённые и ответственные. Полную повестку видят
/// секретарь органа и его члены. Поэтому доступ считается не по заседанию целиком,
/// а по каждому вопросу: на одном заседании сотрудник может видеть один вопрос из десяти.
/// </summary>
public class MeetingAccessService : IMeetingAccessService
{
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MeetingAccessService(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public bool CanManage(MeetingBody body) => _currentUser.HasPermission(PermissionFor(body));

    public bool CanReport => _currentUser.HasPermission(PermissionCode.ReportMeetingExecution);

    /// <summary>
    /// Полную повестку видят секретарь органа и его члены. Общее право доступа к разделу
    /// такого доступа не даёт: сотрудник исполняющего подразделения открывает журнал,
    /// но видит в нём только вопросы, где он назван.
    /// </summary>
    public bool SeesAllItems(MeetingBody body) =>
        CanManage(body) || _currentUser.HasPermission(MembershipFor(body));

    public void RequireManage(MeetingBody body)
    {
        if (!CanManage(body))
            throw new UnauthorizedAccessException(
                $"Ведение заседаний «{MeetingTitles.Body(body)}» доступно секретарю этого органа");
    }

    public async Task<HashSet<int>> VisibleItemIdsAsync(int meetingId, CancellationToken ct = default)
    {
        var body = await _db.Meetings
            .Where(m => m.Id == meetingId)
            .Select(m => (MeetingBody?)m.Body)
            .FirstOrDefaultAsync(ct);

        if (body is null) return [];

        var items = _db.AgendaItems.Where(i => i.MeetingId == meetingId);

        if (SeesAllItems(body.Value))
            return (await items.Select(i => i.Id).ToListAsync(ct)).ToHashSet();

        var me = _currentUser.UserId;

        var visible = await items
            .Where(i =>
                i.SpeakerUserId == me ||
                i.SpeakerHeadUserId == me ||
                i.DeputySecretaryUserId == me ||
                i.ControllerUserId == me ||
                i.Guests.Any(g => g.UserId == me) ||
                i.Assignments.Any(a => a.UserId == me))
            .Select(i => i.Id)
            .ToListAsync(ct);

        return visible.ToHashSet();
    }

    /// <summary>
    /// Право на ведение — своё у каждого органа: секретарь КПА не заводит заседания
    /// Правления, и наоборот. Новые органы добавляются вместе со своим правом.
    /// </summary>
    public static PermissionCode PermissionFor(MeetingBody body) => body switch
    {
        MeetingBody.Board => PermissionCode.ManageBoardMeetings,
        MeetingBody.Kpa => PermissionCode.ManageKpaMeetings,
        MeetingBody.CreditCommittee => PermissionCode.ManageCreditCommitteeMeetings,
        _ => PermissionCode.ManageKpaMeetings,
    };

    /// <summary>Право, которым отмечено членство в органе: по нему рассылаются уведомления.</summary>
    public static PermissionCode MembershipFor(MeetingBody body) => body switch
    {
        MeetingBody.Board => PermissionCode.MemberOfBoard,
        MeetingBody.Kpa => PermissionCode.MemberOfKpa,
        MeetingBody.CreditCommittee => PermissionCode.MemberOfCreditCommittee,
        _ => PermissionCode.MemberOfKpa,
    };
}

/// <summary>Человеческие названия перечислений — одни и те же в карточке, реестре и письмах.</summary>
public static class MeetingTitles
{
    public static string Body(MeetingBody body) => body switch
    {
        MeetingBody.Board => "Правление",
        MeetingBody.Kpa => "КПА",
        MeetingBody.CreditCommittee => "Кредитный комитет",
        _ => body.ToString(),
    };

    /// <summary>Название органа в родительном падеже — для тем и текстов писем.</summary>
    public static string BodyGenitive(MeetingBody body) => body switch
    {
        MeetingBody.Board => "Правления",
        MeetingBody.Kpa => "Комитета по проблемным активам",
        MeetingBody.CreditCommittee => "Кредитного комитета",
        _ => body.ToString(),
    };

    public static string Form(MeetingForm form) => form switch
    {
        MeetingForm.InPerson => "Очно",
        MeetingForm.Absentee => "Заочно",
        _ => form.ToString(),
    };

    public static string Status(ExecutionStatus status) => status switch
    {
        ExecutionStatus.New => "Новое",
        ExecutionStatus.InProgress => "На исполнении",
        ExecutionStatus.DoneOnTime => "Исполнено в срок",
        ExecutionStatus.DoneLate => "Исполнено с нарушением срока",
        ExecutionStatus.NotDone => "Не исполнено",
        ExecutionStatus.Cancelled => "Отменено",
        ExecutionStatus.Excluded => "Исключено/снято из повестки дня заседания",
        _ => status.ToString(),
    };

    public static string FileKind(MeetingFileKind kind) => kind switch
    {
        MeetingFileKind.Sz => "Служебная записка",
        MeetingFileKind.Protocol => "Протокол",
        MeetingFileKind.Execution => "Файл об исполнении",
        _ => kind.ToString(),
    };

    /// <summary>Поручение считается открытым, пока по нему не вынесен окончательный статус.</summary>
    public static bool IsOpen(ExecutionStatus status) =>
        status is ExecutionStatus.New or ExecutionStatus.InProgress;
}
