using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Явка на заседание коллегиального органа.
///
/// Кворум считается по присутствовавшим, и протокол начинается со списка: кто
/// был, кто отсутствовал и почему. Отмечают только отсутствие — заставлять
/// секретаря щёлкать по каждому пришедшему незачем.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MeetingAttendanceTests
{
    private readonly PostgresFixture _postgres;

    public MeetingAttendanceTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task По_умолчанию_присутствуют_все()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var явка = await Состав(db).AttendanceAsync(стенд.MeetingId);

        Assert.Equal(3, явка.Count);
        Assert.All(явка, x => Assert.True(x.Present));
    }

    [Fact]
    public async Task Председатель_идёт_первым()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var явка = await Состав(db).AttendanceAsync(стенд.MeetingId);

        Assert.Equal(стенд.Chairman, явка[0].UserId);
        Assert.Equal("Председатель", явка[0].RoleTitle);
    }

    [Fact]
    public async Task Отсутствие_отмечается_с_причиной()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var явка = await Состав(db).MarkAttendanceAsync(стенд.MeetingId, new AttendanceMarkRequest
        {
            UserId = стенд.Member,
            Present = false,
            Note = "Командировка",
        }, actorUserId: стенд.Secretary);

        var строка = явка.Single(x => x.UserId == стенд.Member);

        Assert.False(строка.Present);
        Assert.Equal("Командировка", строка.Note);
    }

    [Fact]
    public async Task Возврат_присутствия_снимает_причину()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var состав = Состав(db);

        await состав.MarkAttendanceAsync(стенд.MeetingId, new AttendanceMarkRequest
        {
            UserId = стенд.Member, Present = false, Note = "Отпуск",
        }, actorUserId: стенд.Secretary);

        var явка = await состав.MarkAttendanceAsync(стенд.MeetingId, new AttendanceMarkRequest
        {
            UserId = стенд.Member, Present = true,
        }, actorUserId: стенд.Secretary);

        var строка = явка.Single(x => x.UserId == стенд.Member);

        Assert.True(строка.Present);

        // Причина относилась к отсутствию: человек пришёл — причины нет.
        Assert.Null(строка.Note);
    }

    [Fact]
    public async Task Постороннему_явку_не_отметить()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var посторонний = await ПользовательАsync(db, "Не член органа");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Состав(db).MarkAttendanceAsync(стенд.MeetingId, new AttendanceMarkRequest
            {
                UserId = посторонний, Present = false,
            }, actorUserId: стенд.Secretary));
    }

    [Fact]
    public async Task Состав_берётся_на_дату_заседания_а_не_на_сегодня()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // Человека вывели из состава после заседания: в протоколе он остаётся.
        var завтра = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        await db.BodyMembers.Where(m => m.UserId == стенд.Member)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.To, завтра));

        var явка = await Состав(db).AttendanceAsync(стенд.MeetingId);

        Assert.Contains(явка, x => x.UserId == стенд.Member);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int MeetingId, int Chairman, int Member, int Secretary);

    private static IBodyMemberService Состав(DelosferaDbContext db) =>
        new BodyMemberService(db, new BankClock());

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var председатель = await ПользовательАsync(db, "Мукушева Дамира");
        var член = await ПользовательАsync(db, "Артыков Фархад");
        var секретарь = await ПользовательАsync(db, "Секретарь Правления");

        db.BodyMembers.AddRange(
            new BodyMember {Body = MeetingBody.Board, UserId = председатель, Role = BodyRole.Chairman},
            new BodyMember {Body = MeetingBody.Board, UserId = член},
            new BodyMember {Body = MeetingBody.Board, UserId = секретарь, Role = BodyRole.Secretary});

        var meeting = new Meeting
        {
            Body = MeetingBody.Board,
            Form = MeetingForm.InPerson,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Time = new TimeOnly(10, 0),
            Year = 2026,
            Number = 1,
            SecretaryUserId = секретарь,
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        return new Стенд(meeting.Id, председатель, член, секретарь);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"att-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
