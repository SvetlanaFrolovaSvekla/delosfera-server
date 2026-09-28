using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Delosfera.Tests;

/// <summary>
/// Маршрут кадровой СЗ по алгоритму: (директор филиала) → кадровик УЧР →
/// начальник профильного управления (по подразделению сотрудника) → курирующий
/// зампред. Если СЗ создал сам директор — его этап пропускается.
/// </summary>
[Collection(PostgresCollection.Name)]
public class KadrovayaRouteTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Кадровая_с_филиала_идёт_директор_УЧР_начальник_зампред()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var s = await SeedAsync(db, авторДиректор: false);

        var route = await InstantiateAsync(db, s.DocumentId);
        var chain = route.Steps.OrderBy(x => x.Order)
            .Select(x => x.Participants.Single().UserId).ToList();

        Assert.Equal(new int?[] { s.Director, s.BranchHr, s.EmpHead, s.EmpCurator }, chain);
    }

    [Fact]
    public async Task Если_директор_сам_создал_его_этап_пропускается()
    {
        await using var db = await postgres.NewIsolatedDbAsync();
        var s = await SeedAsync(db, авторДиректор: true);

        var route = await InstantiateAsync(db, s.DocumentId);
        var chain = route.Steps.OrderBy(x => x.Order)
            .Select(x => x.Participants.Single().UserId).ToList();

        // Первый шаг (author-head = сам директор-автор) выпал: сразу УЧР → начальник → зампред.
        Assert.Equal(new int?[] { s.BranchHr, s.EmpHead, s.EmpCurator }, chain);
    }

    // ── стенд ──────────────────────────────────────────────────────────────
    private sealed record Стенд(int DocumentId, int Director, int BranchHr, int EmpHead, int EmpCurator);

    private static async Task<RouteInstance> InstantiateAsync(DelosferaDbContext db, int documentId)
    {
        var tpl = new RouteTemplate
        {
            DocumentType = DocumentType.Sz,
            Name = "Кадровая СЗ — типовой",
            Steps =
            [
                new RouteTemplateStep { Order = 1, Kind = StepKind.Approval, Participants = [new() { RoleRef = RouteRoles.AuthorHead, Required = true }] },
                new RouteTemplateStep { Order = 2, Kind = StepKind.Approval, Participants = [new() { RoleRef = RouteRoles.HrOfficer, Required = true }] },
                new RouteTemplateStep { Order = 3, Kind = StepKind.Approval, Participants = [new() { RoleRef = RouteRoles.TargetUnitHead, Required = true }] },
                new RouteTemplateStep { Order = 4, Kind = StepKind.Approval, Participants = [new() { RoleRef = RouteRoles.TargetUnitCurator, Required = true }] },
            ],
        };
        db.RouteTemplates.Add(tpl);
        await db.SaveChangesAsync();

        var engine = new RouteEngine(
            db, new AuditService(db), [], new NoSubstitutions(), new SilentNotifier(),
            new FakeSignatures(), new RouteRoleResolver(db));
        return await engine.InstantiateFromTemplateAsync(documentId, tpl.Id);
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db, bool авторДиректор)
    {
        var director = await UserAsync(db, "Директор филиала");
        var branchHr = await UserAsync(db, "Кадровик УЧР (филиалы)");
        var headOfficeHr = await UserAsync(db, "Кадровик УЧР (ГО)");
        var empHead = await UserAsync(db, "Начальник профильного управления");
        var empCurator = await UserAsync(db, "Курирующий зампред");

        // Подразделение автора — филиал (определяется по слову «Филиал» в названии).
        var branch = new OrganizationUnit { TitleRu = "Филиал Ошский", HeadUserId = director };
        // Подразделение сотрудника — профильное управление: его начальник и куратор.
        var empUnit = new OrganizationUnit { TitleRu = "Управление кредитования", HeadUserId = empHead, CuratorUserId = empCurator };
        db.OrganizationUnits.AddRange(branch, empUnit);
        await db.SaveChangesAsync();

        db.Set<HrRoutingSettings>().Add(new HrRoutingSettings
        {
            BranchHrUserId = branchHr, HeadOfficeHrUserId = headOfficeHr,
        });

        var kind = new SzKind { TitleRu = "Кадровая", FormKey = SzFormKey.Hr };
        var hrKind = new SzHrKind { TitleRu = "Приём" };
        db.SzKinds.Add(kind);
        db.SzHrKinds.Add(hrKind);
        await db.SaveChangesAsync();

        // Автор — либо сам директор филиала, либо рядовой сотрудник филиала.
        var author = авторДиректор
            ? director
            : await UserAsync(db, "Специалист филиала");
        await db.Users.Where(u => u.Id == author)
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.OrgUnitId, branch.Id));

        var doc = new Document { Type = DocumentType.Sz, Title = "Кадровая СЗ", StatusCode = "OnApproval", AuthorId = author };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        db.SzDocuments.Add(new SzDocument
        {
            DocumentId = doc.Id,
            KindId = kind.Id,
            HrKindId = hrKind.Id,
            AuthorUnitId = branch.Id,
            EmployeeUnitId = empUnit.Id,   // профильная структура → Согл.3/4
        });
        await db.SaveChangesAsync();

        return new Стенд(doc.Id, director, branchHr, empHead, empCurator);
    }

    private static async Task<int> UserAsync(DelosferaDbContext db, string fullName)
    {
        var u = new User { FullName = fullName, Email = $"ksz-{Guid.NewGuid():N}@keremetbank.kg", PasswordHash = "x" };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }
}
