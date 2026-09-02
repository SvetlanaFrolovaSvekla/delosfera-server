using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Integrations.Controllers;
using delosfera_server.Modules.Users.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Подразделения, подчинённые человеку, встают под ним.
///
/// Банк устроен так, что часть управлений подчинена не вышестоящему управлению,
/// а заместителю Председателя. Портал это и присылает: вместо вышестоящего
/// подразделения приходит человек. Дерево строилось только по вышестоящему
/// подразделению, и такие узлы всплывали корнями — на стенде 28 штук вперемешку
/// с настоящими верхними, отсортированные по алфавиту.
/// </summary>
[Collection(PostgresCollection.Name)]
public class OrgTreeShapeTests
{
    private readonly PostgresFixture _postgres;

    public OrgTreeShapeTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Подразделение_куратора_встаёт_под_ним()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var дерево = await ДеревоАsync(db);

        var правление = Найти(дерево, стенд.Правление);
        var зампред = правление!.Children.Single(x => x.IsPerson);

        Assert.Equal("Алдибекова Назира", зампред.Title);
        Assert.Contains(зампред.Children, x => x.Id == стенд.БэкОфис);
    }

    [Fact]
    public async Task Куратор_становится_узлом_под_своим_подразделением()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var дерево = await ДеревоАsync(db);

        // Зампред числится в Правлении — там его место. Верхним уровнем он бы
        // оторвался от структуры: подчинённость читается сверху вниз.
        Assert.DoesNotContain(дерево.Roots, x => x.IsPerson);
        Assert.NotNull(Найти(дерево, -стенд.Зампред));
    }

    [Fact]
    public async Task Курируемое_подразделение_не_остаётся_корнем()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var дерево = await ДеревоАsync(db);

        Assert.DoesNotContain(дерево.Roots, x => x.Id == стенд.БэкОфис);
    }

    [Fact]
    public async Task Куратор_своего_же_подразделения_кольца_не_создаёт()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // Председатель числится в Правлении и его же курирует. Повесить Правление
        // под него — замкнуть кольцо, на котором обход дерева не кончится.
        await ПоставитьКуратораАsync(db, стенд.Правление, стенд.Председатель);

        var дерево = await ДеревоАsync(db);

        // Каждое подразделение стоит в дереве ровно один раз, и к ним добавлены
        // узлы кураторов. На кольце этот подсчёт не завершился бы вовсе.
        var кураторов = await db.OrganizationUnits
            .Where(u => u.CuratorUserId != null)
            .Select(u => u.CuratorUserId)
            .Distinct()
            .CountAsync();

        Assert.Equal(дерево.UnitsTotal + кураторов, Узлов(дерево.Roots));
        Assert.Contains(дерево.Roots, x => x.Id == стенд.Правление);
    }

    [Fact]
    public async Task Подразделение_с_вышестоящим_остаётся_на_месте()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var дерево = await ДеревоАsync(db);

        var бэкОфис = Найти(дерево, стенд.БэкОфис);

        Assert.Contains(бэкОфис!.Children, x => x.Id == стенд.Сектор);
    }

    [Fact]
    public async Task Ни_одно_подразделение_из_дерева_не_пропадает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        await SeedAsync(db);

        var дерево = await ДеревоАsync(db);

        // Новый уровень перевешивает узлы с места на место, и потерять при этом
        // подразделение — значит убрать его из структуры совсем.
        var вДереве = new List<int>();
        Собрать(дерево.Roots, вДереве);

        var вБазе = await db.OrganizationUnits.Select(u => u.Id).ToListAsync();

        Assert.Equal(вБазе.OrderBy(x => x), вДереве.Where(x => x > 0).OrderBy(x => x));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(
        int Правление, int БэкОфис, int Сектор, int Зампред, int Председатель);

    private static async Task<OrgTreeResponse> ДеревоАsync(DelosferaDbContext db)
    {
        var результат = await new OrgTreeController(db).Get(CancellationToken.None);
        return Assert.IsType<OrgTreeResponse>(Assert.IsType<OkObjectResult>(результат.Result).Value);
    }

    private static OrgTreeNode? Найти(OrgTreeResponse дерево, int id) => Найти(дерево.Roots, id);

    private static OrgTreeNode? Найти(List<OrgTreeNode> узлы, int id)
    {
        foreach (var узел in узлы)
        {
            if (узел.Id == id) return узел;

            var внутри = Найти(узел.Children, id);
            if (внутри is not null) return внутри;
        }

        return null;
    }

    /// <summary>Считает узлы обходом: на кольце обход не кончится, и тест это покажет.</summary>
    private static int Узлов(List<OrgTreeNode> узлы) =>
        узлы.Count + узлы.Sum(x => Узлов(x.Children));

    private static void Собрать(List<OrgTreeNode> узлы, List<int> куда)
    {
        foreach (var узел in узлы)
        {
            куда.Add(узел.Id);
            Собрать(узел.Children, куда);
        }
    }

    private static async Task ПоставитьКуратораАsync(DelosferaDbContext db, int unitId, int userId)
    {
        var unit = await db.OrganizationUnits.FindAsync(unitId);
        unit!.CuratorUserId = userId;
        await db.SaveChangesAsync();
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var зампред = await ПользовательАsync(db, "Алдибекова Назира");
        var председатель = await ПользовательАsync(db, "Мукушева Дамира");

        var правление = new OrganizationUnit {TitleRu = "Правление", Kind = OrgUnitKind.Board};
        db.OrganizationUnits.Add(правление);
        await db.SaveChangesAsync();

        await db.Users.Where(u => u.Id == зампред || u.Id == председатель)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.OrgUnitId, правление.Id));

        var бэкОфис = new OrganizationUnit
        {
            TitleRu = "Бэк-офис",
            Kind = OrgUnitKind.Division,
            CuratorUserId = зампред,
        };
        db.OrganizationUnits.Add(бэкОфис);
        await db.SaveChangesAsync();

        var сектор = new OrganizationUnit
        {
            TitleRu = "Сектор межбанковских операций",
            Kind = OrgUnitKind.Department,
            ParentId = бэкОфис.Id,
        };
        db.OrganizationUnits.Add(сектор);
        await db.SaveChangesAsync();

        return new Стенд(правление.Id, бэкОфис.Id, сектор.Id, зампред, председатель);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"tree-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
