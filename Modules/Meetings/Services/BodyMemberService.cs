using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.Models;

namespace delosfera_server.Modules.Meetings.Services;

/// <summary>Строка состава органа для экрана настройки.</summary>
public class BodyMemberDto
{
    public int Id { get; set; }
    public MeetingBody Body { get; set; }
    public string BodyTitle { get; set; } = "";

    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string? Position { get; set; }
    public string? OrgUnit { get; set; }

    public BodyRole Role { get; set; }
    public string RoleTitle { get; set; } = "";

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Basis { get; set; }

    /// <summary>Состоит ли сейчас: по датам «с» и «по».</summary>
    public bool IsCurrent { get; set; }
}

public class BodyMemberRequest
{
    public MeetingBody Body { get; set; }
    public int UserId { get; set; }
    public BodyRole Role { get; set; } = BodyRole.Member;
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Basis { get; set; }
}

public interface IBodyMemberService
{
    Task<List<BodyMemberDto>> ListAsync(MeetingBody? body, CancellationToken ct = default);
    Task<BodyMemberDto> AddAsync(BodyMemberRequest request, int actorUserId, CancellationToken ct = default);
    Task<BodyMemberDto> UpdateAsync(int id, BodyMemberRequest request, int actorUserId, CancellationToken ct = default);
    Task RemoveAsync(int id, CancellationToken ct = default);

    /// <summary>Идентификаторы тех, кто состоит в органе на сегодня.</summary>
    Task<List<int>> CurrentMemberIdsAsync(MeetingBody body, CancellationToken ct = default);

    /// <summary>Председатель органа; null — не назначен.</summary>
    Task<int?> ChairmanIdAsync(MeetingBody body, CancellationToken ct = default);
}

/// <summary>
/// Состав коллегиальных органов.
///
/// Состав задаётся списком людей, а не правами роли: банк меняет его решением, и
/// перенастройка доступа тут ни при чём. Прежний способ подводил дважды — роли с
/// полным набором прав делали членами Правления администраторов, а председателя
/// искали по праву «выносить вопрос на орган», которое по работе есть и у
/// администратора системы.
/// </summary>
public class BodyMemberService : IBodyMemberService
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public BodyMemberService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<BodyMemberDto>> ListAsync(MeetingBody? body, CancellationToken ct = default)
    {
        var query = _db.BodyMembers.AsNoTracking()
            .Include(m => m.User).ThenInclude(u => u!.Position)
            .Include(m => m.User).ThenInclude(u => u!.OrgUnit)
            .AsQueryable();

        if (body is { } b) query = query.Where(m => m.Body == b);

        var rows = await query
            // Председатель первым — им список и читают.
            .OrderBy(m => m.Body)
            .ThenBy(m => m.Role == BodyRole.Chairman ? 0 : m.Role == BodyRole.Secretary ? 2 : 1)
            .ThenBy(m => m.User!.FullName)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<BodyMemberDto> AddAsync(
        BodyMemberRequest request, int actorUserId, CancellationToken ct = default)
    {
        await EnsureUserAsync(request.UserId, ct);
        await EnsureSingleChairmanAsync(request.Body, request.Role, null, ct);

        if (await _db.BodyMembers.AnyAsync(m => m.Body == request.Body && m.UserId == request.UserId, ct))
            throw new InvalidOperationException("Этот человек уже числится в составе органа");

        var member = new BodyMember
        {
            Body = request.Body,
            UserId = request.UserId,
            Role = request.Role,
            From = request.From,
            To = request.To,
            Basis = request.Basis?.Trim(),
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.BodyMembers.Add(member);
        await _db.SaveChangesAsync(ct);

        return await LoadAsync(member.Id, ct);
    }

    public async Task<BodyMemberDto> UpdateAsync(
        int id, BodyMemberRequest request, int actorUserId, CancellationToken ct = default)
    {
        var member = await _db.BodyMembers.FirstOrDefaultAsync(m => m.Id == id, ct)
                     ?? throw new KeyNotFoundException("Запись состава не найдена");

        await EnsureSingleChairmanAsync(member.Body, request.Role, id, ct);

        member.Role = request.Role;
        member.From = request.From;
        member.To = request.To;
        member.Basis = request.Basis?.Trim();
        member.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await LoadAsync(id, ct);
    }

    public async Task RemoveAsync(int id, CancellationToken ct = default)
    {
        var member = await _db.BodyMembers.FirstOrDefaultAsync(m => m.Id == id, ct)
                     ?? throw new KeyNotFoundException("Запись состава не найдена");

        _db.BodyMembers.Remove(member);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<int>> CurrentMemberIdsAsync(MeetingBody body, CancellationToken ct = default)
    {
        var today = _clock.Today;

        return await _db.BodyMembers.AsNoTracking()
            .Where(m => m.Body == body
                        && (m.From == null || m.From <= today)
                        && (m.To == null || m.To >= today))
            .Select(m => m.UserId)
            .ToListAsync(ct);
    }

    public async Task<int?> ChairmanIdAsync(MeetingBody body, CancellationToken ct = default)
    {
        var today = _clock.Today;

        return await _db.BodyMembers.AsNoTracking()
            .Where(m => m.Body == body
                        && m.Role == BodyRole.Chairman
                        && (m.From == null || m.From <= today)
                        && (m.To == null || m.To >= today))
            .Select(m => (int?) m.UserId)
            .FirstOrDefaultAsync(ct);
    }

    // ── вспомогательное ──────────────────────────────────────────────────────

    /// <summary>Председатель в органе один: второй означал бы два первых голоса.</summary>
    private async Task EnsureSingleChairmanAsync(
        MeetingBody body, BodyRole role, int? exceptId, CancellationToken ct)
    {
        if (role != BodyRole.Chairman) return;

        var busy = await _db.BodyMembers
            .Where(m => m.Body == body && m.Role == BodyRole.Chairman)
            .Where(m => exceptId == null || m.Id != exceptId)
            .Include(m => m.User)
            .FirstOrDefaultAsync(ct);

        if (busy is not null)
            throw new InvalidOperationException(
                $"Председатель уже назначен — {busy.User?.FullName}. Снимите прежнего, прежде чем назначать нового.");
    }

    private async Task EnsureUserAsync(int userId, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
            throw new KeyNotFoundException("Пользователь не найден");
    }

    private async Task<BodyMemberDto> LoadAsync(int id, CancellationToken ct)
    {
        var member = await _db.BodyMembers.AsNoTracking()
            .Include(m => m.User).ThenInclude(u => u!.Position)
            .Include(m => m.User).ThenInclude(u => u!.OrgUnit)
            .FirstAsync(m => m.Id == id, ct);

        return ToDto(member);
    }

    private BodyMemberDto ToDto(BodyMember m)
    {
        var today = _clock.Today;

        return new BodyMemberDto
        {
            Id = m.Id,
            Body = m.Body,
            BodyTitle = MeetingTitles.Body(m.Body),
            UserId = m.UserId,
            UserName = m.User?.FullName ?? "",
            Position = m.User?.Position?.TitleRu,
            OrgUnit = m.User?.OrgUnit?.TitleRu,
            Role = m.Role,
            RoleTitle = RoleTitle(m.Role),
            From = m.From,
            To = m.To,
            Basis = m.Basis,
            IsCurrent = (m.From is null || m.From <= today) && (m.To is null || m.To >= today),
        };
    }

    private static string RoleTitle(BodyRole role) => role switch
    {
        BodyRole.Chairman => "Председатель",
        BodyRole.Secretary => "Секретарь",
        _ => "Член органа",
    };
}
