using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

/// <summary>
/// Членство в коллегиальном органе — состав органа, а не право доступа.
///
/// Роли администратора и главного редактора ВНД заводились полным перечнем прав,
/// а в перечне лежали признаки членства. Оттого членами Правления числились
/// айтишники и методологи: в поле «Кому» записки на Правление предлагались они, а
/// председателя, зампредов и члена Правления-главного бухгалтера там не было.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BoardMembershipTests
{
    private readonly PostgresFixture _postgres;

    public BoardMembershipTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Администратор_не_член_Правления()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var администратор = await db.Roles.AsNoTracking().FirstAsync(r => r.Id == 1);

        Assert.DoesNotContain((int) PermissionCode.MemberOfBoard, администратор.PermissionCodes);
    }

    [Fact]
    public async Task Ни_одна_сидовая_роль_не_раздаёт_признаки_членства()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var признаки = new[]
        {
            (int) PermissionCode.MemberOfBoard,
            (int) PermissionCode.MemberOfKpa,
            (int) PermissionCode.MemberOfCreditCommittee,
        };

        var роли = await db.Roles.AsNoTracking().ToListAsync();

        // Состав органа задаётся ролью органа, а не набором прав должности.
        var нарушители = роли
            .Where(r => r.PermissionCodes.Intersect(признаки).Any())
            .Where(r => !r.TitleRu.Contains("Правлени", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.TitleRu)
            .ToList();

        Assert.Empty(нарушители);
    }

    [Fact]
    public async Task Правление_признак_получает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        db.Roles.Add(new Role {TitleRu = "Правление", TitleEn = "Board", PermissionCodes = []});
        await db.SaveChangesAsync();

        await RolePermissionDefaults.ApplyAsync(db, NullLogger.Instance);

        var правление = await db.Roles.AsNoTracking().FirstAsync(r => r.TitleRu == "Правление");

        Assert.Contains((int) PermissionCode.MemberOfBoard, правление.PermissionCodes);
        Assert.Contains((int) PermissionCode.ViewAllSz, правление.PermissionCodes);
    }

    [Fact]
    public async Task Председатель_получает_и_членство_и_право_выносить_на_орган()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        db.Roles.Add(new Role
        {
            TitleRu = "Председатель Правления",
            TitleEn = "Chairman of the Management Board",
            PermissionCodes = [],
        });
        await db.SaveChangesAsync();

        await RolePermissionDefaults.ApplyAsync(db, NullLogger.Instance);

        var председатель = await db.Roles.AsNoTracking()
            .FirstAsync(r => r.TitleRu == "Председатель Правления");

        Assert.Contains((int) PermissionCode.MemberOfBoard, председатель.PermissionCodes);
        Assert.Contains((int) PermissionCode.SubmitSzToBody, председатель.PermissionCodes);
    }

    [Fact]
    public async Task Признак_членства_не_достаётся_роли_редактора_ВНД()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        var редактор = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TitleRu == "Главный редактор ВНД");

        Assert.NotNull(редактор);
        Assert.DoesNotContain((int) PermissionCode.MemberOfBoard, редактор!.PermissionCodes);
    }
}
