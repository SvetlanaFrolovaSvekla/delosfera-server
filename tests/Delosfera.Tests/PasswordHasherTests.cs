using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;

namespace Delosfera.Tests;

public class PasswordHasherTests
{
    private readonly UserPasswordHasher _hasher = new();

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("S3cret!");
        Assert.True(_hasher.Verify(hash, "S3cret!"));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("S3cret!");
        Assert.False(_hasher.Verify(hash, "wrong"));
    }

    [Fact]
    public void Hash_IsNotPlaintext_AndSalted()
    {
        var h1 = _hasher.Hash("same");
        var h2 = _hasher.Hash("same");
        Assert.NotEqual("same", h1);
        Assert.NotEqual(h1, h2); // соль → разные хеши одного пароля
    }

    [Theory]
    [InlineData("")]
    [InlineData("!invalidated!")]
    [InlineData("not-base64-хеш")]
    public void Verify_EmptyOrCorruptHash_ReturnsFalse_DoesNotThrow(string badHash)
    {
        // Инвалидированные сид-аккаунты: проверка не должна падать исключением.
        Assert.False(_hasher.Verify(badHash, "anything"));
    }
}
