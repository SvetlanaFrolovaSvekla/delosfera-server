using Microsoft.AspNetCore.Identity;

namespace delosfera_server.Common.Services;

public interface IUserPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public class UserPasswordHasher : IUserPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new object(), password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed;
}