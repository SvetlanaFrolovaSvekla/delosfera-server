using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

public class VndServiceTests
{
    private const int Creator = 100;

    private static VndService NewService(DelosferaDbContext db, int currentUserId, params delosfera_server.Modules.Users.Models.PermissionCode[] perms) =>
        new(db, new NoopFileStorage(), new FakeCurrentUser(currentUserId, perms));

    private static VndDocument SeedVnd(DelosferaDbContext db, VndStatus status)
    {
        var vnd = new VndDocument
        {
            Code = "TEST-10001",
            TitleRu = "Тестовый ВНД",
            Status = status,
            CreatedByUserId = Creator,
        };
        db.VndDocuments.Add(vnd);
        db.SaveChanges();
        return vnd;
    }

    [Fact]
    public async Task Delete_DraftByCreator_RemovesDocument()
    {
        using var db = TestSupport.NewDb();
        var vnd = SeedVnd(db, VndStatus.Draft);
        var svc = NewService(db, Creator);

        await svc.DeleteAsync(vnd.Id, Creator);

        Assert.False(await db.VndDocuments.AnyAsync(x => x.Id == vnd.Id));
    }

    [Fact]
    public async Task Delete_NonDraft_Throws()
    {
        using var db = TestSupport.NewDb();
        var vnd = SeedVnd(db, VndStatus.Active);
        var svc = NewService(db, Creator);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteAsync(vnd.Id, Creator));
        Assert.True(await db.VndDocuments.AnyAsync(x => x.Id == vnd.Id)); // не удалён
    }

    [Fact]
    public async Task Delete_ByOtherUserWithoutPrivilege_Throws()
    {
        using var db = TestSupport.NewDb();
        var vnd = SeedVnd(db, VndStatus.Draft);
        var svc = NewService(db, 200); // не создатель, без прав главреда

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteAsync(vnd.Id, 200));
    }

    [Fact]
    public async Task Delete_UnknownId_Throws()
    {
        using var db = TestSupport.NewDb();
        var svc = NewService(db, Creator);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.DeleteAsync(999, Creator));
    }
}
