using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Documents.VND.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delosfera.Tests;

[Collection(PostgresCollection.Name)]
public class VndServiceTests
{
    private readonly PostgresFixture _postgres;

    public VndServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private const int Creator = 100;

    private static VndService NewService(DelosferaDbContext db, int currentUserId, params delosfera_server.Modules.Users.Models.PermissionCode[] perms) =>
        new(db, new NoopFileStorage(), new FakeCurrentUser(currentUserId, perms), new FakeActivityLog(),
            new VndApprovalService(
                db, new NoopFileStorage(), new NoopNotificationService(),
                new FakeCurrentUser(currentUserId, perms),
                NullLogger<VndApprovalService>.Instance, new FakeActivityLog(),
                new ApprovalSheetGenerator(), new FixedApprovalUnitResolver(db)),
            new delosfera_server.Modules.Documents.Services.NumeratorService(db));

    private static VndDocument SeedVnd(DelosferaDbContext db, VndStatus status) =>
        TestSupport.SeedVnd(db, status, Creator);

    [Fact]
    public async Task Delete_DraftByCreator_RemovesDocument()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var vnd = SeedVnd(db, VndStatus.Draft);
        var svc = NewService(db, Creator);

        await svc.DeleteAsync(vnd.Id, Creator);

        Assert.False(await db.VndDocuments.AnyAsync(x => x.Id == vnd.Id));
    }

    [Fact]
    public async Task Delete_NonDraft_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var vnd = SeedVnd(db, VndStatus.Active);
        var svc = NewService(db, Creator);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteAsync(vnd.Id, Creator));
        Assert.True(await db.VndDocuments.AnyAsync(x => x.Id == vnd.Id)); // не удалён
    }

    [Fact]
    public async Task Delete_ByOtherUserWithoutPrivilege_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var vnd = SeedVnd(db, VndStatus.Draft);
        var svc = NewService(db, 200); // не создатель, без прав главреда

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteAsync(vnd.Id, 200));
    }

    [Fact]
    public async Task Delete_UnknownId_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var svc = NewService(db, Creator);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.DeleteAsync(999, Creator));
    }
}
