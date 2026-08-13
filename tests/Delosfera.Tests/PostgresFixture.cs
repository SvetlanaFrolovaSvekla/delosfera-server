using delosfera_server.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Delosfera.Tests;

/// <summary>
/// Настоящий Postgres в контейнере на время тестов.
///
/// In-memory провайдер модель этой системы не выдерживает: в ней jsonb, tsvector,
/// вычисляемые колонки и значения по умолчанию на стороне базы. Подгонять модель под
/// него — значит вносить в продуктовый код правки ради тестов и всё равно проверять
/// не то поведение, которое будет в банке.
///
/// Контейнер поднимается один на весь прогон: старт Postgres дороже самих тестов.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("delosfera_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Схему накатываем миграциями, а не EnsureCreated: тесты должны идти по той же
        // схеме, что и промышленная база, включая вычисляемые колонки и индексы.
        await using var db = NewDb();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public DelosferaDbContext NewDb() =>
        new(new DbContextOptionsBuilder<DelosferaDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>
    /// Очищает таблицы перед тестом, оставляя справочники из миграций. Пересоздавать
    /// базу на каждый тест дороже, а общий контейнер без очистки даёт зависимость
    /// тестов друг от друга — самый неприятный вид ложных падений.
    /// </summary>
    public async Task ResetAsync(params string[] tables)
    {
        if (tables.Length == 0) return;

        await using var db = NewDb();
        var list = string.Join(", ", tables.Select(t => $"\"{t}\""));

        await db.Database.ExecuteSqlRawAsync($"TRUNCATE {list} RESTART IDENTITY CASCADE");
    }
}

/// <summary>Общий контейнер на все тесты, которым нужна база.</summary>
[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
