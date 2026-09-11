using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using delosfera_server.Common.Security;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Integrations.OrgStructure;
using delosfera_server.Modules.Users.Models;

namespace Delosfera.Tests;

/// <summary>
/// Отключение уволенных при синхронизации оргструктуры.
///
/// Это единственное, что синхронизация делает необратимого для человека: после
/// него он не войдёт в систему. Поэтому проверяется и то, что уволенных гасят,
/// и то, что массовое отключение не проходит: сбой портала не должен закрыть
/// доступ всему банку, а возвращать его пятистам сотрудникам вручную — рабочий день.
/// </summary>
[Collection(PostgresCollection.Name)]
public class OrgSyncDeactivationTests(PostgresFixture postgres)
{
    /// <summary>Своя приставка на каждый прогон: почта у пользователя уникальна.</summary>
    private readonly string _метка = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Свой номер подразделения: сопоставление идёт по нему.</summary>
    private readonly int _внешнийНомер = Random.Shared.Next(90_000, 99_999);

    private DbContextOptions<DelosferaDbContext> Options() =>
        new DbContextOptionsBuilder<DelosferaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

    /// <summary>
    /// Портал, отвечающий заранее заданным набором. Настоящий по сети недоступен
    /// из среды сборки, да и проверяем мы не его, а наше решение об отключении.
    /// </summary>
    private sealed class StubPortal(List<PortalUnit> units, List<PortalEmployee> employees)
        : PortalOrgClient(new HttpClient())
    {
        public override Task<List<PortalUnit>> GetUnitsAsync(string _, string __, CancellationToken ___ = default)
            => Task.FromResult(units);

        public override Task<List<PortalEmployee>> GetEmployeesAsync(string _, string __, CancellationToken ___ = default)
            => Task.FromResult(employees);
    }

    private async Task<(int unitId, List<int> userIds)> ЗавестиЛюдей(int сколько)
    {
        await using var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0));

        var now = DateTime.UtcNow;
        var unit = new OrganizationUnit
        {
            TitleRu = $"Отдел для проверки отключения {_метка}",
            ExternalId = _внешнийНомер,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.OrganizationUnits.Add(unit);
        await db.SaveChangesAsync();

        var ids = new List<int>();
        for (var i = 0; i < сколько; i++)
        {
            var user = new User
            {
                FullName = $"Проверочный Сотрудник {i}",
                Email = $"proverka-{_метка}-{i}@keremetbank.kg",
                LdapLogin = $"proverka-{_метка}-{i}",
                PasswordHash = "x",
                IsActive = true,
                OrgUnitId = unit.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            ids.Add(user.Id);
        }

        return (unit.Id, ids);
    }

    /// <summary>Настройки из одного значения: шифрователю нужен только Jwt:Key.</summary>
    private sealed class КлючИзПамяти : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key]
        {
            get => key == "Jwt:Key" ? "проверочный-ключ-длиннее-тридцати-двух-символов" : null;
            set => throw new NotSupportedException();
        }

        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotSupportedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotSupportedException();
    }

    private PortalEmployee Сотрудник(int i, bool работает) => new()
    {
        Login = $"proverka-{_метка}-{i}",
        Name = $"Проверочный Сотрудник {i}",
        Email = $"proverka-{_метка}-{i}@keremetbank.kg",
        Unit = new PortalUnitRef { Id = _внешнийНомер, Name = "Отдел для проверки отключения" },
        Active = работает,
    };

    private async Task<OrgSyncRun> Синхронизировать(List<PortalEmployee> изПортала)
    {
        await using var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0));

        var настройки = await db.OrgStructureSettings.FirstOrDefaultAsync();
        var protector = new SecretProtector(new КлючИзПамяти());

        if (настройки is null)
        {
            db.OrgStructureSettings.Add(new OrgStructureSettings
            {
                Enabled = true,
                PortalUrl = "https://portal.test/api/v1",
                TokenEncrypted = protector.Protect("khb_proverochnyj"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var units = new List<PortalUnit>
        {
            new() { Id = _внешнийНомер, Name = $"Отдел для проверки отключения {_метка}", Kind = "department" },
        };

        var service = new OrgSyncService(
            db, new StubPortal(units, изПортала), protector,
            NullLogger<OrgSyncService>.Instance);

        return await service.RunAsync(startedByUserId: null);
    }

    [Fact]
    public async Task Уволенного_в_портале_гасим_и_здесь()
    {
        var (_, ids) = await ЗавестиЛюдей(6);

        // Один уволен, остальные работают — обычная убыль, не сбой.
        var изПортала = Enumerable.Range(0, 6)
            .Select(i => Сотрудник(i, работает: i != 0))
            .ToList();

        var run = await Синхронизировать(изПортала);

        Assert.Equal(1, run.EmployeesDeactivated);

        await using var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0));
        var уволенный = await db.Users.FirstAsync(u => u.Id == ids[0]);
        var работающий = await db.Users.FirstAsync(u => u.Id == ids[1]);

        Assert.False(уволенный.IsActive);
        Assert.True(работающий.IsActive);
    }

    [Fact]
    public async Task Массовое_отключение_не_проходит_и_остаётся_замечание()
    {
        var (_, ids) = await ЗавестиЛюдей(10);

        // Портал пометил уволенными почти всех. В банке столько разом
        // не увольняют — вернее, что портал ответил неправдой.
        var изПортала = Enumerable.Range(0, 10)
            .Select(i => Сотрудник(i, работает: i == 0))
            .ToList();

        var run = await Синхронизировать(изПортала);

        Assert.Equal(0, run.EmployeesDeactivated);
        Assert.Contains("Доступ никому не закрыт", run.NotesJson ?? "");

        await using var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0));
        var сколькоРаботает = await db.Users.CountAsync(u => ids.Contains(u.Id) && u.IsActive);

        Assert.Equal(10, сколькоРаботает);
    }

    [Fact]
    public async Task Руководитель_из_карточки_проставляется()
    {
        var (_, ids) = await ЗавестиЛюдей(3);

        var изПортала = Enumerable.Range(0, 3)
            .Select(i => Сотрудник(i, работает: true))
            .ToList();

        // Второму назначен личный руководитель — первый. По правилу портала
        // он важнее начальника подразделения.
        изПортала[1].Manager = new PortalPerson
        {
            Login = $"proverka-{_метка}-0",
            Name = "Проверочный Сотрудник 0",
        };

        await Синхронизировать(изПортала);

        await using var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0));
        var подчинённый = await db.Users.FirstAsync(u => u.Id == ids[1]);
        var безРуководителя = await db.Users.FirstAsync(u => u.Id == ids[2]);

        Assert.Equal(ids[0], подчинённый.ManagerId);
        Assert.Null(безРуководителя.ManagerId);
    }

    [Fact]
    public async Task Сам_себе_руководителем_не_становится()
    {
        var (_, ids) = await ЗавестиЛюдей(2);

        var изПортала = Enumerable.Range(0, 2)
            .Select(i => Сотрудник(i, работает: true))
            .ToList();

        // Портал по ошибке указал человека руководителем самого себя. Обход
        // цепочки согласования по такой связи не кончился бы никогда.
        изПортала[0].Manager = new PortalPerson
        {
            Login = $"proverka-{_метка}-0",
            Name = "Проверочный Сотрудник 0",
        };

        var run = await Синхронизировать(изПортала);

        await using var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0));
        var сам = await db.Users.FirstAsync(u => u.Id == ids[0]);

        Assert.Null(сам.ManagerId);
        Assert.Contains("руководителем указан он сам", run.NotesJson ?? "");
    }

    [Fact]
    public async Task Вид_подразделения_переносится_из_портала()
    {
        await ЗавестиЛюдей(1);
        await Синхронизировать([Сотрудник(0, работает: true)]);

        await using var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0));
        var unit = await db.OrganizationUnits.FirstAsync(u => u.ExternalId == _внешнийНомер);

        // В заглушке портала вид указан department
        Assert.Equal(OrgUnitKind.Department, unit.Kind);
    }

    [Fact]
    public async Task Вернувшегося_на_работу_включаем_обратно()
    {
        var (_, ids) = await ЗавестиЛюдей(4);

        await using (var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0)))
        {
            var user = await db.Users.FirstAsync(u => u.Id == ids[0]);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var изПортала = Enumerable.Range(0, 4)
            .Select(i => Сотрудник(i, работает: true))
            .ToList();

        await Синхронизировать(изПортала);

        await using var проверка = new DelosferaDbContext(Options(), new FakeCurrentUser(0));
        var вернувшийся = await проверка.Users.FirstAsync(u => u.Id == ids[0]);

        Assert.True(вернувшийся.IsActive);
    }

    [Fact]
    public async Task Главенство_из_HeadsUnit_проставляет_руководителя()
    {
        var (unitId, ids) = await ЗавестиЛюдей(2);

        // Портал указывает главенство на самом сотруднике (HeadsUnit), а не на
        // подразделении (unit.Head). Прежде этот источник игнорировался, и
        // подразделение оставалось без руководителя.
        var изПортала = new List<PortalEmployee>
        {
            new()
            {
                Login = $"proverka-{_метка}-0",
                Name = "Проверочный Сотрудник 0",
                Email = $"proverka-{_метка}-0@keremetbank.kg",
                Unit = new PortalUnitRef { Id = _внешнийНомер, Name = "Отдел" },
                HeadsUnit = new PortalUnitRef { Id = _внешнийНомер, Name = "Отдел" },
                Active = true,
            },
            Сотрудник(1, работает: true),
        };

        await Синхронизировать(изПортала);

        await using var db = new DelosferaDbContext(Options(), new FakeCurrentUser(0));
        var unit = await db.OrganizationUnits.FirstAsync(u => u.Id == unitId);
        Assert.Equal(ids[0], unit.HeadUserId);
    }
}
