using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.ActivityLog.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Журнал действий по документу — из технического аудита, для всех контуров.
///
/// Аудит писали все контуры, а человекочитаемый журнал был только у ВНД. История
/// строится из того же аудита на лету, поэтому её получает каждый контур сразу:
/// записка, закупка, договор, письмо.
/// </summary>
[Collection(PostgresCollection.Name)]
public class DocumentHistoryTests
{
    private readonly PostgresFixture _postgres;

    public DocumentHistoryTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task История_записки_читается_из_аудита()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await ПользовательАsync(db, "Иванов Иван");
        var audit = new AuditService(db);

        await audit.LogAsync("Sz", 7, "Created", автор);
        await audit.LogAsync("Sz", 7, "SubmittedForApproval", автор);
        await audit.LogAsync("Sz", 7, "Registered", автор);

        var история = await Сервис(db).ForAsync("Sz", 7);

        Assert.Equal(3, история.Count);
        // Новое сверху.
        Assert.Equal("Registered", история[0].Action);
        Assert.Equal("Иванов Иван зарегистрировал(а) записку", история[0].Text);
    }

    [Fact]
    public async Task Действие_подставляет_фамилию_актёра()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await ПользовательАsync(db, "Петров Пётр");

        await new AuditService(db).LogAsync("ProcurementContract", 3, "Terminated", автор);

        var история = await Сервис(db).ForAsync("ProcurementContract", 3);

        Assert.Equal("Петров Пётр расторг(ла) договор", история.Single().Text);
    }

    [Fact]
    public async Task Системное_событие_без_актёра_подписывается_Системой()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();

        // Автоакцепт по таймауту — событие без человека.
        await new AuditService(db).LogAsync("Sz", 9, "StatusFromRoute", userId: null);

        var история = await Сервис(db).ForAsync("Sz", 9);

        Assert.Equal("Система", история.Single().ActorName);
        Assert.StartsWith("Система ", история.Single().Text);
    }

    [Fact]
    public async Task Поручения_записки_попадают_в_её_историю()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await ПользовательАsync(db, "Автор");
        var исполнитель = await ПользовательАsync(db, "Исполнитель");
        var audit = new AuditService(db);

        await audit.LogAsync("Sz", 5, "Resolution", автор);
        await audit.LogAsync("SzAssignment", 88, "Reported", исполнитель);

        var история = await Сервис(db).ForAsync("Sz", 5, related: [("SzAssignment", 88)]);

        Assert.Equal(2, история.Count);
        Assert.Contains(история, e => e.Text == "Исполнитель отчитался(ась) по поручению");
    }

    [Fact]
    public async Task Чужой_документ_того_же_типа_в_историю_не_попадает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await ПользовательАsync(db, "Автор");
        var audit = new AuditService(db);

        await audit.LogAsync("Sz", 1, "Created", автор);
        await audit.LogAsync("Sz", 2, "Created", автор);

        var история = await Сервис(db).ForAsync("Sz", 1);

        Assert.Single(история);
    }

    [Fact]
    public async Task Неизвестное_действие_показывается_как_есть_а_не_пропадает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var автор = await ПользовательАsync(db, "Автор");

        await new AuditService(db).LogAsync("Sz", 4, "СовсемНовоеДействие", автор);

        var история = await Сервис(db).ForAsync("Sz", 4);

        // Журнал не молчит о том, чего не знает: показывает код действия.
        Assert.Contains("СовсемНовоеДействие", история.Single().Text);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private static IDocumentHistoryService Сервис(DelosferaDbContext db) =>
        new DocumentHistoryService(db);

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"hist-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
