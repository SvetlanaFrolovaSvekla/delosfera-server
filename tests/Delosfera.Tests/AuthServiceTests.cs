using System.Security.Cryptography;
using System.Text;
using delosfera_server.Modules.Users.DTO.Request;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

[Collection(PostgresCollection.Name)]
public class AuthServiceTests
{
    private readonly PostgresFixture _postgres;

    public AuthServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private static string Sha256Hex(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    private static LoginRequest Login(string email, string pwd) => new() { Email = email, Password = pwd };

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsTokensAndUser()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.SeedUser(db, "user@bank.kg", "P@ssw0rd");
        var auth = TestSupport.NewAuthService(db);

        var result = await auth.LoginAsync(Login("user@bank.kg", "P@ssw0rd"), "ru");

        Assert.False(string.IsNullOrWhiteSpace(result.Response.Token));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal("user@bank.kg", result.Response.User.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.SeedUser(db, "user@bank.kg", "P@ssw0rd");
        var auth = TestSupport.NewAuthService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.LoginAsync(Login("user@bank.kg", "nope"), "ru"));
    }

    [Fact]
    public async Task Login_UnknownEmail_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var auth = TestSupport.NewAuthService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.LoginAsync(Login("ghost@bank.kg", "x"), "ru"));
    }

    [Fact]
    public async Task Login_InactiveUser_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.SeedUser(db, "user@bank.kg", "P@ssw0rd", isActive: false);
        var auth = TestSupport.NewAuthService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.LoginAsync(Login("user@bank.kg", "P@ssw0rd"), "ru"));
    }

    [Fact]
    public async Task Login_BlockedUser_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.SeedUser(db, "user@bank.kg", "P@ssw0rd", blockedAt: DateTime.UtcNow);
        var auth = TestSupport.NewAuthService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.LoginAsync(Login("user@bank.kg", "P@ssw0rd"), "ru"));
    }

    [Fact]
    public async Task Login_StoresRefreshTokenHashed_NotPlaintext()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.SeedUser(db, "user@bank.kg", "P@ssw0rd");
        var auth = TestSupport.NewAuthService(db);

        var result = await auth.LoginAsync(Login("user@bank.kg", "P@ssw0rd"), "ru");

        var stored = await db.Tokens.SingleAsync();
        Assert.NotEqual(result.RefreshToken, stored.RefreshTokenHash);       // не открытым текстом
        Assert.Equal(Sha256Hex(result.RefreshToken), stored.RefreshTokenHash); // именно SHA-256 хеш
        Assert.True(stored.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesAndRevokesOld()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.SeedUser(db, "user@bank.kg", "P@ssw0rd");
        var auth = TestSupport.NewAuthService(db);

        var first = await auth.LoginAsync(Login("user@bank.kg", "P@ssw0rd"), "ru");
        var second = await auth.RefreshAsync(first.RefreshToken, "ru");

        Assert.False(string.IsNullOrWhiteSpace(second.RefreshToken));
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        var oldHash = Sha256Hex(first.RefreshToken);
        var old = await db.Tokens.SingleAsync(t => t.RefreshTokenHash == oldHash);
        Assert.True(old.IsLoggedOut); // старый refresh отозван
    }

    [Fact]
    public async Task Refresh_UnknownToken_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var auth = TestSupport.NewAuthService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.RefreshAsync("never-issued", "ru"));
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.SeedUser(db, "user@bank.kg", "P@ssw0rd");
        var auth = TestSupport.NewAuthService(db);

        var login = await auth.LoginAsync(Login("user@bank.kg", "P@ssw0rd"), "ru");
        var token = await db.Tokens.SingleAsync();
        token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.RefreshAsync(login.RefreshToken, "ru"));
    }

    [Fact]
    public async Task Logout_ThenRefresh_Throws()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        TestSupport.SeedUser(db, "user@bank.kg", "P@ssw0rd");
        var auth = TestSupport.NewAuthService(db);

        var login = await auth.LoginAsync(Login("user@bank.kg", "P@ssw0rd"), "ru");
        await auth.LogoutAsync(login.RefreshToken);

        var token = await db.Tokens.SingleAsync();
        Assert.True(token.IsLoggedOut);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => auth.RefreshAsync(login.RefreshToken, "ru"));
    }
}
