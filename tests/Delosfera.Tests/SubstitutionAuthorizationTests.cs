using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Users.DTO;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;

namespace Delosfera.Tests;

/// <summary>
/// Авторизация замещений (GEN-14, легаси /api/substitutions).
///
/// Замещение перенаправляет чужие задачи и согласования замещающему
/// (GetActingForUserIdsAsync). Без проверки владельца любой сотрудник оформил бы
/// замещение на жертву с собой в замещающих и перехватил её поручения. Поэтому
/// оформить и отменить замещение может только его участник — либо кадровик с
/// правом ManageUsers за любого.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SubstitutionAuthorizationTests
{
    private readonly PostgresFixture _postgres;

    public SubstitutionAuthorizationTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Нельзя_оформить_замещение_за_другого()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.EnsureUser(db, ЖертваId);
        TestSupport.EnsureUser(db, ЗлоумышленникId);

        // Злоумышленник пытается перенаправить задачи жертвы на себя.
        var service = Service(db, актор: ЗлоумышленникId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(Заявка(замещаемый: ЖертваId, замещающий: ЗлоумышленникId), ЗлоумышленникId));

        Assert.False(db.Substitutions.Any());
    }

    [Fact]
    public async Task Можно_оформить_замещение_на_себя()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.EnsureUser(db, ЖертваId);
        TestSupport.EnsureUser(db, ЗлоумышленникId);

        var service = Service(db, актор: ЖертваId);

        var dto = await service.CreateAsync(
            Заявка(замещаемый: ЖертваId, замещающий: ЗлоумышленникId), ЖертваId);

        Assert.Equal(ЖертваId, dto.UserId);
        Assert.Equal(ЗлоумышленникId, dto.SubstituteUserId);
    }

    [Fact]
    public async Task Кадровик_с_ManageUsers_оформляет_за_любого()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.EnsureUser(db, ЖертваId);
        TestSupport.EnsureUser(db, ЗамещающийId);
        TestSupport.EnsureUser(db, КадровикId);

        var service = Service(db, актор: КадровикId, PermissionCode.ManageUsers);

        var dto = await service.CreateAsync(
            Заявка(замещаемый: ЖертваId, замещающий: ЗамещающийId), КадровикId);

        Assert.Equal(ЖертваId, dto.UserId);
        Assert.Equal(ЗамещающийId, dto.SubstituteUserId);
    }

    [Fact]
    public async Task Нельзя_отменить_чужое_замещение()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.EnsureUser(db, ЖертваId);
        TestSupport.EnsureUser(db, ЗамещающийId);
        TestSupport.EnsureUser(db, ЗлоумышленникId);

        var созданное = await Service(db, актор: ЖертваId).CreateAsync(
            Заявка(замещаемый: ЖертваId, замещающий: ЗамещающийId), ЖертваId);

        // Посторонний не участвует в замещении и права ManageUsers не имеет.
        var service = Service(db, актор: ЗлоумышленникId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CancelAsync(созданное.Id, ЗлоумышленникId));

        Assert.False(db.Substitutions.Single(s => s.Id == созданное.Id).IsCancelled);
    }

    [Fact]
    public async Task Участник_может_отменить_своё_замещение()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.EnsureUser(db, ЖертваId);
        TestSupport.EnsureUser(db, ЗамещающийId);

        var созданное = await Service(db, актор: ЖертваId).CreateAsync(
            Заявка(замещаемый: ЖертваId, замещающий: ЗамещающийId), ЖертваId);

        // Замещающий — тоже участник, отменить вправе.
        var dto = await Service(db, актор: ЗамещающийId).CancelAsync(созданное.Id, ЗамещающийId);

        Assert.True(dto.IsCancelled);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private const int ЖертваId = 9001;
    private const int ЗлоумышленникId = 9002;
    private const int ЗамещающийId = 9003;
    private const int КадровикId = 9004;

    private static SubstitutionService Service(
        DelosferaDbContext db, int актор, params PermissionCode[] права) =>
        new(db, new AuditService(db), new BankClock(), new FakeCurrentUser(актор, права));

    private static SubstitutionCreateRequest Заявка(int замещаемый, int замещающий) =>
        new()
        {
            UserId = замещаемый,
            SubstituteUserId = замещающий,
            StartsOn = new DateOnly(2026, 1, 10),
            EndsOn = new DateOnly(2026, 1, 20),
            Reason = "отпуск",
        };
}
