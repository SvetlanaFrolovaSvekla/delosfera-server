using delosfera_server.Data;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

/// <summary>
/// Кто держит права на записки.
///
/// Право можно завести, закрыть им кнопку и не выдать ни одной роли. Действие
/// тогда закрыто для всех, включая тех, ради кого оно писалось, а отличить это
/// от исправной работы по журналу нельзя: ошибок нет, просто никто не нажимает.
/// Так уже случилось с вынесением записки на коллегиальный орган.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RolePermissionDefaultsTests
{
    private readonly PostgresFixture _postgres;

    public RolePermissionDefaultsTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Каждое_право_записок_кому_то_принадлежит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        await ЗавестиРолиИРаздатьПраваАsync(db);

        // Ровно та проверка, которой не было: право без держателя — закрытая кнопка.
        foreach (var право in new[]
                 {
                     PermissionCode.ViewAllSz,
                     PermissionCode.RegisterSz,
                     PermissionCode.SubmitSzToBody,
                 })
        {
            var держатели = await ДержателиАsync(db, право);

            Assert.True(держатели.Count > 0,
                $"Право «{право}» не выдано ни одной роли: действие закрыто для всех");
        }
    }

    [Fact]
    public async Task Каждое_право_закупок_кому_то_принадлежит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        await ЗавестиРолиИРаздатьПраваАsync(db);

        // Кроме администратора: право, которое держит только он, означает, что
        // контур работает лишь под администратором — то есть не работает.
        foreach (var право in new[]
                 {
                     PermissionCode.ViewAllProcurements,
                     PermissionCode.ConductProcurement,
                     PermissionCode.RecordCommissionDecisions,
                     PermissionCode.ManageProcurementProtocol,
                     PermissionCode.ManageProcurementContracts,
                     PermissionCode.ManageProcurementPlan,
                     PermissionCode.ManageSuppliers,
                 })
        {
            var держатели = (await ДержателиАsync(db, право))
                .Where(t => t != "Администратор")
                .ToList();

            Assert.True(держатели.Count > 0,
                $"Право «{право}» есть только у администратора: работать в контуре некому");
        }
    }

    [Fact]
    public async Task Правление_видит_реестр_но_на_орган_выносит_только_председатель()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        await ЗавестиРолиИРаздатьПраваАsync(db);

        var правление = await РольАsync(db, "Правление");
        var председатель = await РольАsync(db, "Председатель Правления");

        Assert.Contains((int)PermissionCode.ViewAllSz, правление.PermissionCodes);
        Assert.Contains((int)PermissionCode.ViewAllSz, председатель.PermissionCodes);

        // Зампред видит всё, но вынести вопрос на коллегиальный орган не вправе.
        Assert.DoesNotContain((int)PermissionCode.SubmitSzToBody, правление.PermissionCodes);
        Assert.Contains((int)PermissionCode.SubmitSzToBody, председатель.PermissionCodes);
    }

    [Fact]
    public async Task Регистрирует_записку_только_делопроизводство()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        await ЗавестиРолиИРаздатьПраваАsync(db);

        var держатели = await ДержателиАsync(db, PermissionCode.RegisterSz);

        // Номер записке присваивает канцелярия. Правление регистрировать не должно:
        // иначе номер появляется в обход книги регистрации.
        Assert.DoesNotContain("Правление", держатели);
        Assert.DoesNotContain("Председатель Правления", держатели);
        Assert.Contains("Сектор делопроизводства", держатели);
    }

    [Fact]
    public async Task Повторный_запуск_не_плодит_роли()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        await ЗавестиРолиИРаздатьПраваАsync(db);
        await ЗавестиРолиИРаздатьПраваАsync(db);

        var правлений = await db.Roles.CountAsync(r => r.TitleRu == "Правление");

        Assert.Equal(1, правлений);
    }

    [Fact]
    public async Task Роль_для_обкатки_не_подменяется_боевой()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        db.Roles.Add(new Role {TitleRu = "Обкатка: Правление", PermissionCodes = []});
        await db.SaveChangesAsync();

        await ЗавестиРолиИРаздатьПраваАsync(db);

        // Демонстрационная роль названа похоже, но это не состав Правления банка:
        // боевая роль должна появиться отдельно.
        Assert.Equal(1, await db.Roles.CountAsync(r => r.TitleRu == "Правление"));
        Assert.Equal(1, await db.Roles.CountAsync(r => r.TitleRu == "Обкатка: Правление"));
    }

    [Fact]
    public async Task Настроенное_банком_не_отбирается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        db.Roles.Add(new Role
        {
            TitleRu = "Правление",
            PermissionCodes = [(int)PermissionCode.ManageSystemSettings],
        });
        await db.SaveChangesAsync();

        await ЗавестиРолиИРаздатьПраваАsync(db);

        var правление = await РольАsync(db, "Правление");

        // Умолчание добавляет недостающее, но не переопределяет ручную настройку.
        Assert.Contains((int)PermissionCode.ManageSystemSettings, правление.PermissionCodes);
        Assert.Contains((int)PermissionCode.ViewAllSz, правление.PermissionCodes);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static async Task ЗавестиРолиИРаздатьПраваАsync(DelosferaDbContext db)
    {
        await CoreRolesSeeder.ApplyAsync(db, NullLogger.Instance);
        await RolePermissionDefaults.ApplyAsync(db, NullLogger.Instance);
    }

    private static async Task<Role> РольАsync(DelosferaDbContext db, string titleRu) =>
        await db.Roles.AsNoTracking().FirstAsync(r => r.TitleRu == titleRu);

    /// <summary>Названия ролей, которым принадлежит право.</summary>
    private static async Task<List<string>> ДержателиАsync(DelosferaDbContext db, PermissionCode code)
    {
        var roles = await db.Roles.AsNoTracking().ToListAsync();

        return roles
            .Where(r => r.PermissionCodes.Contains((int)code))
            .Select(r => r.TitleRu)
            .ToList();
    }
}
