using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using User = delosfera_server.Modules.Users.Models.User;

namespace Delosfera.Tests;

/// <summary>
/// Кому сообщают об исходе согласования.
///
/// Задание банка: «уведомления должны отправляться всем — исполнителю,
/// создателю, кто регистрирует, начальник СП». Раньше уходило одному автору,
/// и начальник узнавал об отклонении документа своего подразделения
/// от подчинённого.
///
/// Проверяется именно круг адресатов, а не текст письма: текст перепишут,
/// а вот незаметно сузившийся круг — та поломка, которую никто не заметит,
/// пока начальник не спросит, почему его не известили.
/// </summary>
[Collection(PostgresCollection.Name)]
public class OutcomeNotificationTests(PostgresFixture postgres)
{
    private DbContextOptions<DelosferaDbContext> Options() =>
        new DbContextOptionsBuilder<DelosferaDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

    private DelosferaDbContext Context() => new(Options(), new FakeCurrentUser(0));

    /// <summary>
    /// Запоминает, кому ушло. Наследуется от пустой заглушки: проверяем один
    /// метод, а остальные девять интерфейса нас не касаются.
    /// </summary>
    private sealed class ЗапоминающаяСлужба : NoopNotificationService
    {
        public List<int> Получатели { get; } = [];

        public override Task<int> CreateAsync(CreateNotificationRequest request, int? currentUserId)
        {
            Получатели.AddRange(request.UserIds);
            return Task.FromResult(1);
        }
    }

    /// <summary>
    /// Заводит подразделение с начальником, автора в нём и записку с маршрутом.
    /// Возвращает всех, кто должен получить весть об исходе.
    /// </summary>
    private async Task<(int routeInstanceId, int авторId, int начальникId, int регистраторId)>
        ЗавестиЗаписку(DelosferaDbContext db)
    {
        var метка = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        var начальник = new User
        {
            FullName = $"Начальник {метка}", Email = $"nach-{метка}@keremetbank.kg",
            PasswordHash = "x", IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        var регистратор = new User
        {
            FullName = $"Делопроизводитель {метка}", Email = $"reg-{метка}@keremetbank.kg",
            PasswordHash = "x", IsActive = true, CreatedAt = now, UpdatedAt = now,
        };
        db.Users.AddRange(начальник, регистратор);
        await db.SaveChangesAsync();

        var подразделение = new OrganizationUnit
        {
            TitleRu = $"Отдел {метка}", HeadUserId = начальник.Id,
            CreatedAt = now, UpdatedAt = now,
        };
        db.OrganizationUnits.Add(подразделение);
        await db.SaveChangesAsync();

        var автор = new User
        {
            FullName = $"Автор {метка}", Email = $"avtor-{метка}@keremetbank.kg",
            PasswordHash = "x", IsActive = true, OrgUnitId = подразделение.Id,
            CreatedAt = now, UpdatedAt = now,
        };
        db.Users.Add(автор);
        await db.SaveChangesAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = $"Записка {метка}",
            AuthorId = автор.Id,
            StatusCode = "Draft",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        db.SzDocuments.Add(new delosfera_server.Modules.Sz.Models.SzDocument
        {
            DocumentId = document.Id,
            KindId = db.Set<delosfera_server.Modules.Sz.Models.SzKind>().OrderBy(k => k.Id).First().Id,
            RegisteredByUserId = регистратор.Id,
        });

        var instance = new RouteInstance {DocumentId = document.Id};
        db.RouteInstances.Add(instance);
        await db.SaveChangesAsync();

        return (instance.Id, автор.Id, начальник.Id, регистратор.Id);
    }

    [Fact]
    public async Task Об_отклонении_узнают_автор_начальник_и_регистратор()
    {
        await using var db = Context();
        var (routeId, автор, начальник, регистратор) = await ЗавестиЗаписку(db);

        var служба = new ЗапоминающаяСлужба();
        var notifier = new WorkflowNotifier(db, служба, NullLogger<WorkflowNotifier>.Instance);

        await notifier.RouteFinishedAsync(routeId, RouteInstanceStatus.Rejected, "не согласовано");

        Assert.Contains(автор, служба.Получатели);
        Assert.Contains(начальник, служба.Получатели);
        Assert.Contains(регистратор, служба.Получатели);
    }

    [Fact]
    public async Task О_завершении_согласования_тоже_сообщают_всем()
    {
        await using var db = Context();
        var (routeId, автор, начальник, регистратор) = await ЗавестиЗаписку(db);

        var служба = new ЗапоминающаяСлужба();
        var notifier = new WorkflowNotifier(db, служба, NullLogger<WorkflowNotifier>.Instance);

        await notifier.RouteFinishedAsync(routeId, RouteInstanceStatus.Approved, null);

        Assert.Contains(автор, служба.Получатели);
        Assert.Contains(начальник, служба.Получатели);
        Assert.Contains(регистратор, служба.Получатели);
    }

    [Fact]
    public async Task Промежуточные_переходы_никого_не_беспокоят()
    {
        await using var db = Context();
        var (routeId, _, _, _) = await ЗавестиЗаписку(db);

        var служба = new ЗапоминающаяСлужба();
        var notifier = new WorkflowNotifier(db, служба, NullLogger<WorkflowNotifier>.Instance);

        // Маршрут пошёл дальше, но не кончился. Письмо на каждый шаг быстро
        // приучает не читать письма вовсе.
        await notifier.RouteFinishedAsync(routeId, RouteInstanceStatus.Running, null);

        Assert.Empty(служба.Получатели);
    }
}
