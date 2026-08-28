using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Settings.Models;

namespace Delosfera.Tests;

/// <summary>
/// Журнал изменений настроек.
///
/// Проверяется не то, что запись появилась, а то, ради чего журнал заводят:
/// видно ли, кто менял, что именно и на что. И то, что журнал не мешает работать
/// фоновым службам, у которых текущего пользователя нет.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SettingsChangeLogTests
{
    private readonly PostgresFixture _postgres;

    public SettingsChangeLogTests(PostgresFixture postgres) => _postgres = postgres;

    private DbContextOptions<DelosferaDbContext> Options() =>
        new DbContextOptionsBuilder<DelosferaDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

    /// <summary>Контекст с текущим пользователем — как в обычном запросе.</summary>
    private DelosferaDbContext ContextAs(int userId) =>
        new(Options(), new FakeCurrentUser(userId));

    [Fact]
    public async Task Заведение_справочника_попадает_в_журнал()
    {
        await using var db = ContextAs(77);

        db.Positions.Add(new Position { TitleRu = "Ведущий специалист" });
        await db.SaveChangesAsync();

        var change = await db.SettingsChanges
            .OrderByDescending(c => c.Id)
            .FirstAsync(c => c.EntityType == nameof(Position));

        Assert.Equal(SettingsChangeKind.Added, change.Kind);
        Assert.Equal("Должности", change.Area);
        Assert.Equal("Ведущий специалист", change.EntityTitle);
        Assert.Equal(77, change.UserId);

        // Идентификатор проставлен после сохранения: до него его не существует.
        Assert.True(change.EntityId > 0);
    }

    [Fact]
    public async Task Правка_записывает_старое_и_новое_значение()
    {
        await using var db = ContextAs(77);

        var position = new Position { TitleRu = "Было" };
        db.Positions.Add(position);
        await db.SaveChangesAsync();

        position.TitleRu = "Стало";
        await db.SaveChangesAsync();

        var change = await db.SettingsChanges
            .Where(c => c.EntityType == nameof(Position)
                        && c.EntityId == position.Id
                        && c.Kind == SettingsChangeKind.Modified)
            .OrderByDescending(c => c.Id)
            .FirstAsync();

        Assert.NotNull(change.ChangesJson);
        Assert.Contains("Было", change.ChangesJson);
        Assert.Contains("Стало", change.ChangesJson);
    }

    [Fact]
    public async Task Удаление_сохраняет_название_удалённого()
    {
        await using var db = ContextAs(77);

        var position = new Position { TitleRu = "Упразднённая должность" };
        db.Positions.Add(position);
        await db.SaveChangesAsync();

        var id = position.Id;
        db.Positions.Remove(position);
        await db.SaveChangesAsync();

        var change = await db.SettingsChanges
            .Where(c => c.EntityType == nameof(Position)
                        && c.EntityId == id
                        && c.Kind == SettingsChangeKind.Deleted)
            .FirstAsync();

        // Ради этого название и хранится снимком: «удалили должность № 47»
        // ничего не объясняет.
        Assert.Equal("Упразднённая должность", change.EntityTitle);
    }

    [Fact]
    public async Task Сохранение_без_изменений_журнал_не_засоряет()
    {
        await using var db = ContextAs(77);

        var position = new Position { TitleRu = "Неизменная" };
        db.Positions.Add(position);
        await db.SaveChangesAsync();

        var before = await db.SettingsChanges.CountAsync(c => c.EntityId == position.Id);

        // Переприсваиваем то же значение: EF пометит поле изменённым, но по сути
        // не изменилось ничего.
        position.TitleRu = "Неизменная";
        await db.SaveChangesAsync();

        var after = await db.SettingsChanges.CountAsync(c => c.EntityId == position.Id);

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Документы_в_журнал_настроек_не_попадают()
    {
        await using var db = ContextAs(77);

        var before = await db.SettingsChanges.CountAsync();

        db.Positions.Add(new Position { TitleRu = "Должность для проверки" });
        await db.SaveChangesAsync();

        var afterDictionary = await db.SettingsChanges.CountAsync();
        Assert.Equal(before + 1, afterDictionary);

        // Журнал настроек следит за справочниками, а не за всем подряд: поток
        // документных событий на порядок больше и утопил бы в себе одну правку
        // справочника, ради которой журнал и завели.
        var kinds = await db.SettingsChanges.Select(c => c.EntityType).Distinct().ToListAsync();
        Assert.DoesNotContain("Document", kinds);
        Assert.DoesNotContain("SzDocument", kinds);
    }

    [Fact]
    public async Task Без_текущего_пользователя_сохранение_не_падает()
    {
        // Так контекст поднимают фоновые службы: чистка журнала посещений,
        // закрытие истёкших доверенностей, календарь обязательств. Обращение к
        // текущему пользователю там бросает исключение, и журнал не должен
        // ронять из-за этого само сохранение.
        await using var db = new DelosferaDbContext(Options());

        db.Positions.Add(new Position { TitleRu = "Заведено фоновой службой" });

        var saved = await db.SaveChangesAsync();

        Assert.True(saved > 0);
    }
}
