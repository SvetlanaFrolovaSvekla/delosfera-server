using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;

namespace Delosfera.Tests;

/// <summary>
/// Кому видна задача на консолидацию.
///
/// Проверяется одно: список задач и право на публикацию сходятся. Разойдясь,
/// они дают худший вид поломки — задача висит у человека, который не может её
/// выполнить, и выглядит это как «система сломалась», а не как «не ваша задача».
///
/// Так и случилось на обкатке 24 августа: список отдавал задачу создателю
/// документа, а опубликовать мог инициатор согласования. Сотрудница написала
/// через «Сообщить»: «висит ВНД, но кнопки по консолидации нет».
/// </summary>
[Collection(PostgresCollection.Name)]
public class ConsolidationTaskVisibilityTests(PostgresFixture postgres)
{
    private DbContextOptions<DelosferaDbContext> Options() =>
        new DbContextOptionsBuilder<DelosferaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

    private DelosferaDbContext Context() => new(Options(), new FakeCurrentUser(0));

    /// <summary>Заводит редакцию с процессом согласования от заданного инициатора.</summary>
    private static void ЗавестиРедакциюСИнициатором(
        DelosferaDbContext db, VndDocument vnd, int инициаторId)
    {
        TestSupport.EnsureUser(db, инициаторId);

        // Файл редакции обязателен: на настоящей базе внешний ключ проверяется.
        var file = TestSupport.SeedFile(db, инициаторId);

        var redaction = new VndRedaction
        {
            VndId = vnd.Id,
            Number = 1,
            Code = $"Р-{Guid.NewGuid():N}"[..8],
            TitleRu = "Редакция для проверки консолидации",
            OrganId = db.ApprovalBodies.OrderBy(x => x.Id).First().Id,
            DeveloperId = db.OrganizationUnits.OrderBy(x => x.Id).First().Id,
            SecrecyLevelId = db.SecurityLevels.OrderBy(x => x.Id).First().Id,
            TypeId = db.TypesVnd.OrderBy(x => x.Id).First().Id,
            DocFileRuId = file.Id,
        };
        db.VndRedactions.Add(redaction);
        db.SaveChanges();

        db.Set<VndApprovalProcess>().Add(new VndApprovalProcess
        {
            VndId = vnd.Id,
            RedactionId = redaction.Id,
            InitiatorUserId = инициаторId,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Без_цикла_актуализации_задачу_видит_инициатор_согласования()
    {
        const int создатель = 4101;
        const int инициатор = 4102;

        await using var db = Context();
        var vnd = TestSupport.SeedVnd(db, VndStatus.Consolidation, создатель);
        ЗавестиРедакциюСИнициатором(db, vnd, инициатор);

        var service = new TasksService(db);

        var уИнициатора = await service.GetConsolidationTasksAsync(инициатор);
        var уСоздателя = await service.GetConsolidationTasksAsync(создатель);

        Assert.Contains(уИнициатора, t => t.VndId == vnd.Id);

        // Создатель документа задачу не видит: опубликовать он всё равно не сможет,
        // а висящая невыполнимая задача — это и есть жалоба с обкатки.
        Assert.DoesNotContain(уСоздателя, t => t.VndId == vnd.Id);
    }

    [Fact]
    public async Task В_цикле_актуализации_задачу_видит_ответственный()
    {
        const int создатель = 4201;
        const int ответственный = 4202;
        const int инициатор = 4203;

        await using var db = Context();
        var vnd = TestSupport.SeedVnd(db, VndStatus.Consolidation, создатель);
        ЗавестиРедакциюСИнициатором(db, vnd, инициатор);

        TestSupport.EnsureUser(db, ответственный);
        vnd.ActualizationResponsibleUserId = ответственный;
        await db.SaveChangesAsync();

        var service = new TasksService(db);

        Assert.Contains(await service.GetConsolidationTasksAsync(ответственный),
                        t => t.VndId == vnd.Id);

        // Есть назначенный ответственный — инициатор согласования отходит в сторону.
        Assert.DoesNotContain(await service.GetConsolidationTasksAsync(инициатор),
                              t => t.VndId == vnd.Id);
    }

    [Fact]
    public async Task Документ_не_на_консолидации_в_задачи_не_попадает()
    {
        const int инициатор = 4301;

        await using var db = Context();
        var vnd = TestSupport.SeedVnd(db, VndStatus.Active, инициатор);
        ЗавестиРедакциюСИнициатором(db, vnd, инициатор);

        var service = new TasksService(db);

        Assert.DoesNotContain(await service.GetConsolidationTasksAsync(инициатор),
                              t => t.VndId == vnd.Id);
    }
}
