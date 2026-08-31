using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Sz.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Круги доступа к запискам действуют и в поиске.
///
/// Правило видимости стояло в реестре, а поиск шёл мимо него: по одному слову из
/// текста любой сотрудник читал любую записку — о переводе, об окладе, о
/// взыскании. Ограничение, которое обходится строкой поиска, ничего не
/// ограничивает, поэтому правило вынесено в одно место на оба пути.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SearchVisibilityTests
{
    private readonly PostgresFixture _postgres;

    public SearchVisibilityTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Посторонний_чужую_записку_не_находит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.Посторонний);

        Assert.DoesNotContain("Записка чужого отдела", видно);
    }

    [Fact]
    public async Task Автор_свою_находит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.Автор);

        Assert.Contains("Записка чужого отдела", видно);
    }

    [Fact]
    public async Task Согласующий_находит_то_что_ему_пришло()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.Согласующий);

        Assert.Contains("Записка чужого отдела", видно);
    }

    [Fact]
    public async Task Руководитель_находит_записки_своего_подразделения()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.Руководитель);

        Assert.Contains("Записка чужого отдела", видно);
    }

    [Fact]
    public async Task Право_видеть_все_снимает_ограничение()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var видно = await НайтиАsync(db, стенд.Посторонний, canSeeAll: true);

        Assert.Contains("Записка чужого отдела", видно);
    }

    [Fact]
    public async Task Чужой_черновик_в_поиске_не_показывается()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        // Даже тому, кто видит весь реестр: черновик — ещё не документ.
        var видно = await НайтиАsync(db, стенд.Посторонний, canSeeAll: true);

        Assert.DoesNotContain("Чужой черновик", видно);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int Автор, int Согласующий, int Руководитель, int Посторонний);

    /// <summary>Названия записок, доступных пользователю по правилу видимости.</summary>
    private static async Task<List<string>> НайтиАsync(
        DelosferaDbContext db, int userId, bool canSeeAll = false)
    {
        var query = await SzVisibility.ApplyAsync(
            db.SzDocuments.Include(s => s.Document).AsNoTracking(),
            db, userId, canSeeAll, canSeeOthersDrafts: false);

        return await query.Select(s => s.Document!.Title).ToListAsync();
    }

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var автор = await ПользовательАsync(db, "Автор записки");
        var согласующий = await ПользовательАsync(db, "Согласующий");
        var руководитель = await ПользовательАsync(db, "Начальник отдела");
        var посторонний = await ПользовательАsync(db, "Посторонний сотрудник");

        var отдел = new OrganizationUnit {TitleRu = "Отдел кадров", HeadUserId = руководитель};
        db.OrganizationUnits.Add(отдел);
        await db.SaveChangesAsync();

        await db.Users.Where(u => u.Id == автор)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.OrgUnitId, отдел.Id));

        var записка = await ЗапискаАsync(db, "Записка чужого отдела", автор, отдел.Id, SzStatus.OnApproval);
        await ЗапискаАsync(db, "Чужой черновик", автор, отдел.Id, SzStatus.Draft);

        db.SzApprovers.Add(new SzApprover {SzDocumentId = записка, UserId = согласующий, Order = 1});
        await db.SaveChangesAsync();

        return new Стенд(автор, согласующий, руководитель, посторонний);
    }

    private static async Task<int> ЗапискаАsync(
        DelosferaDbContext db, string title, int authorId, int unitId, string status)
    {
        var kind = await db.SzKinds.AsNoTracking().FirstAsync();

        var document = new Document
        {
            Type = DocumentType.Sz,
            Title = title,
            StatusCode = status,
            AuthorId = authorId,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        var sz = new SzDocument
        {
            DocumentId = document.Id,
            KindId = kind.Id,
            AuthorUnitId = unitId,
            Body = "Текст записки об изменении оклада",
        };
        db.SzDocuments.Add(sz);
        await db.SaveChangesAsync();

        return sz.Id;
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"search-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
