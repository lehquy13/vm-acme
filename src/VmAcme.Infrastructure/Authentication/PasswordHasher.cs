using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VmAcme.Users;

namespace VmAcme.Authentication;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new(Options.Create(new PasswordHasherOptions
        { IterationCount = 210_000 }));

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string password, string hash) =>
        _hasher.VerifyHashedPassword(null!, hash, password) is not PasswordVerificationResult.Failed;
}