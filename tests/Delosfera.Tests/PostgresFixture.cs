using delosfera_server.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    private const string TemplateDatabase = "delosfera_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase(TemplateDatabase)
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>Строка подключения к базе-шаблону: миграции накатаны, данные — только сидовые.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Схему накатываем миграциями, а не EnsureCreated: тесты должны идти по той же
        // схеме, что и промышленная база, включая вычисляемые колонки и индексы.
        await using (var db = NewDb())
        {
            await db.Database.MigrateAsync();
        }

        // Копия базы делается только при отсутствии подключений к шаблону,
        // а пул Npgsql держит их открытыми и после Dispose контекста.
        NpgsqlConnection.ClearAllPools();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Контекст к базе-шаблону. Для тестов, которым хватает общих данных.</summary>
    public DelosferaDbContext NewDb() => NewDb(ConnectionString);

    /// <summary>
    /// Отдельная база под один тест — копия шаблона.
    ///
    /// Postgres копирует базу пофайлово, поэтому это быстрее повторного прогона
    /// миграций, а тест получает чистое состояние и не зависит от соседей: самый
    /// неприятный вид ложных падений — когда тест краснеет из-за данных чужого теста.
    /// </summary>
    public async Task<DelosferaDbContext> NewIsolatedDbAsync()
    {
        var name = $"t_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(ConnectionString))
        {
            await admin.OpenAsync();

            await using var command = admin.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateDatabase}\"";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) {Database = name};
        return NewDb(builder.ConnectionString);
    }

    /// <summary>
    /// Ещё один контекст к той же базе. Нужен там, где проверяется работа двух
    /// параллельных сессий с одной записью.
    /// </summary>
    public DelosferaDbContext NewDbFor(DelosferaDbContext existing) =>
        NewDb(existing.Database.GetConnectionString()!);

    private static DelosferaDbContext NewDb(string connectionString) =>
        new(new DbContextOptionsBuilder<DelosferaDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);
}

/// <summary>Общий контейнер на все тесты, которым нужна база.</summary>
[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
