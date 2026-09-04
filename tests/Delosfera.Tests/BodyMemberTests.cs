using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Состав коллегиального органа — отдельная настройка.
///
/// Раньше состав задавался правами роли, и это подводило дважды: роли с полным
/// набором прав делали членами Правления администраторов и редакторов ВНД, а
/// председателя искали по праву «выносить вопрос на орган» — оно по работе есть
/// и у администратора системы, и он вставал первым в списке «Кому».
/// </summary>
[Collection(PostgresCollection.Name)]
public class BodyMemberTests
{
    private readonly PostgresFixture _postgres;

    public BodyMemberTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Человек_вводится_в_состав()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var председатель = await ПользовательАsync(db, "Мукушева Дамира");

        await Состав(db).AddAsync(new BodyMemberRequest
        {
            Body = MeetingBody.Board,
            UserId = председатель,
            Role = BodyRole.Chairman,
            Basis = "Протокол № 49(14)",
        }, actorUserId: 1);

        Assert.Equal(председатель, await Состав(db).ChairmanIdAsync(MeetingBody.Board));
    }

    [Fact]
    public async Task Председатель_в_органе_один()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var состав = Состав(db);
        var первый = await ПользовательАsync(db, "Первый председатель");
        var второй = await ПользовательАsync(db, "Второй председатель");

        await состав.AddAsync(new BodyMemberRequest
        {
            Body = MeetingBody.Board, UserId = первый, Role = BodyRole.Chairman,
        }, actorUserId: 1);

        // Два председателя означали бы два первых голоса.
        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => состав.AddAsync(new BodyMemberRequest
            {
                Body = MeetingBody.Board, UserId = второй, Role = BodyRole.Chairman,
            }, actorUserId: 1));

        Assert.Contains("Первый председатель", ошибка.Message);
    }

    [Fact]
    public async Task Дважды_в_один_орган_не_вводится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var состав = Состав(db);
        var человек = await ПользовательАsync(db, "Член Правления");

        await состав.AddAsync(new BodyMemberRequest
        {
            Body = MeetingBody.Board, UserId = человек,
        }, actorUserId: 1);

        // Две записи означали бы два голоса и два уведомления.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => состав.AddAsync(new BodyMemberRequest
            {
                Body = MeetingBody.Board, UserId = человек,
            }, actorUserId: 1));
    }

    [Fact]
    public async Task В_разные_органы_один_человек_входит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var состав = Состав(db);
        var человек = await ПользовательАsync(db, "Зампред");

        await состав.AddAsync(new BodyMemberRequest {Body = MeetingBody.Board, UserId = человек}, 1);
        await состав.AddAsync(new BodyMemberRequest {Body = MeetingBody.Kpa, UserId = человек}, 1);

        Assert.Contains(человек, await состав.CurrentMemberIdsAsync(MeetingBody.Board));
        Assert.Contains(человек, await состав.CurrentMemberIdsAsync(MeetingBody.Kpa));
    }

    [Fact]
    public async Task Выбывший_из_состава_в_текущих_не_числится()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var состав = Состав(db);
        var бывший = await ПользовательАsync(db, "Выбывший член");

        await состав.AddAsync(new BodyMemberRequest
        {
            Body = MeetingBody.Board,
            UserId = бывший,
            To = ВчераАsync(),
            Basis = "Выведен из состава решением собрания",
        }, actorUserId: 1);

        // Запись остаётся — по ней видно, кто и когда состоял, — но уведомления
        // и голоса на неё больше не приходятся.
        Assert.DoesNotContain(бывший, await состав.CurrentMemberIdsAsync(MeetingBody.Board));
        Assert.Single(await состав.ListAsync(MeetingBody.Board));
    }

    [Fact]
    public async Task Прежний_председатель_снимается_и_назначается_новый()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var состав = Состав(db);
        var прежний = await ПользовательАsync(db, "Прежний председатель");
        var новый = await ПользовательАsync(db, "Новый председатель");

        var запись = await состав.AddAsync(new BodyMemberRequest
        {
            Body = MeetingBody.Board, UserId = прежний, Role = BodyRole.Chairman,
        }, actorUserId: 1);

        await состав.UpdateAsync(запись.Id, new BodyMemberRequest {Role = BodyRole.Member}, actorUserId: 1);

        await состав.AddAsync(new BodyMemberRequest
        {
            Body = MeetingBody.Board, UserId = новый, Role = BodyRole.Chairman,
        }, actorUserId: 1);

        Assert.Equal(новый, await состав.ChairmanIdAsync(MeetingBody.Board));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static IBodyMemberService Состав(DelosferaDbContext db) =>
        new BodyMemberService(db, new BankClock());

    private static DateOnly ВчераАsync() =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"body-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
