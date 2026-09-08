using delosfera_server.Data;
using delosfera_server.Modules.Hr.Models;
using delosfera_server.Modules.Search.DTO;
using delosfera_server.Modules.Search.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Кадровые приказы в поиске — и правило доступа вместе с ними.
///
/// Приказ по личному составу несёт оклад, взыскание, причину увольнения. До сих
/// пор он в поиск не входил вовсе, и это скорее защищало. Раз входит — закрыт
/// тем же правилом, что и книга приказов: кадровая служба видит всё, остальные —
/// только приказы о себе.
/// </summary>
[Collection(PostgresCollection.Name)]
public class HrOrderSearchTests
{
    private readonly PostgresFixture _postgres;

    public HrOrderSearchTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Кадровик_находит_чужой_приказ()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var найдено = await НайтиАsync(db, стенд.Кадровик, "взыскание", ViewsAll: true);

        Assert.Contains("О дисциплинарном взыскании", найдено);
    }

    [Fact]
    public async Task Посторонний_чужой_приказ_не_находит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // Одно слово из текста чужого приказа не должно его открывать.
        var найдено = await НайтиАsync(db, стенд.Посторонний, "взыскание", ViewsAll: false);

        Assert.Empty(найдено);
    }

    [Fact]
    public async Task Сотрудник_находит_приказ_о_себе()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var найдено = await НайтиАsync(db, стенд.Наказанный, "взыскание", ViewsAll: false);

        Assert.Contains("О дисциплинарном взыскании", найдено);
    }

    [Fact]
    public async Task Проект_приказа_виден_только_кадровой_службе()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // Пока приказ не подписан, решения ещё нет — сотруднику показывать нечего,
        // даже если приказ о нём.
        var сотрудником = await НайтиАsync(db, стенд.Наказанный, "проект", ViewsAll: false);
        var кадровиком = await НайтиАsync(db, стенд.Кадровик, "проект", ViewsAll: true);

        Assert.Empty(сотрудником);
        Assert.Contains("Проект приказа об увольнении", кадровиком);
    }

    [Fact]
    public async Task Поиск_идёт_и_по_номеру_приказа()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        Assert.Contains("О дисциплинарном взыскании",
            await НайтиАsync(db, стенд.Кадровик, "7-лс", ViewsAll: true));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int Кадровик, int Наказанный, int Посторонний);

    /// <summary>Названия приказов, которые пользователь находит по запросу.</summary>
    private static async Task<List<string>> НайтиАsync(
        DelosferaDbContext db, int userId, string query, bool ViewsAll)
    {
        var currentUser = ViewsAll
            ? new FakeCurrentUser(userId, PermissionCode.ViewHrOrders)
            : new FakeCurrentUser(userId);

        var service = new SearchService(
            db, new delosfera_server.Modules.Meetings.Services.MeetingAccessService(db, currentUser),
            currentUser);

        var result = await service.SearchAsync(new SearchRequest
        {
            Query = query,
            Scopes = [SearchScope.HrOrder],
        }, userId);

        return result.Items.Select(i => i.Title).ToList();
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var кадровик = await ПользовательАsync(db, "Кадровик");
        var наказанный = await ПользовательАsync(db, "Наказанный сотрудник");
        var посторонний = await ПользовательАsync(db, "Посторонний сотрудник");

        db.HrOrders.AddRange(
            new HrOrder
            {
                Kind = HrOrderKind.Discipline,
                Status = HrOrderStatus.Signed,
                Title = "О дисциплинарном взыскании",
                Body = "Объявить замечание за нарушение сроков",
                Year = 2026,
                RegNumber = "7-лс",
                OrderDate = new DateOnly(2026, 9, 1),
                CreatedByUserId = кадровик,
                Employees = [new HrOrderEmployee {UserId = наказанный}],
            },
            new HrOrder
            {
                Kind = HrOrderKind.Dismissal,
                Status = HrOrderStatus.Draft,
                Title = "Проект приказа об увольнении",
                Body = "Проект: расторгнуть трудовой договор",
                Year = 2026,
                OrderDate = new DateOnly(2026, 9, 3),
                CreatedByUserId = кадровик,
                Employees = [new HrOrderEmployee {UserId = наказанный}],
            });

        await db.SaveChangesAsync();

        return new Стенд(кадровик, наказанный, посторонний);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"hrs-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
