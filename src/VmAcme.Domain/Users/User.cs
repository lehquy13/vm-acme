using VmAcme.Common;

namespace VmAcme.Users;

public sealed class User : AggregateRoot<Guid>
{
    private User()
    {
    }

    public string Email { get; private set; } = "";
    public string NormalizedEmail { get; private set; } = "";
    public string FirstName { get; private set; } = "";
    public string LastName { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public static User Create(string email, string firstName, string lastName, string passwordHash) => new()
    {
        Id = Guid.NewGuid(), Email = email.Trim(), NormalizedEmail = NormalizeEmail(email),
        FirstName = firstName.Trim(), LastName = lastName.Trim(), PasswordHash = passwordHash
    };
}