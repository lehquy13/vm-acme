using VmAcme.Users;

namespace VmAcme.Authentication;

public interface IUserContext
{
    Guid? UserId { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public sealed record AccessToken(string AccessTokenValue, DateTimeOffset ExpiresAt);

public interface ITokenProvider
{
    AccessToken Create(User user);
}

public interface IPermissionProvider
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken);
}

public static class Permissions
{
    public const string UsersRead = "users:read";
    public const string UsersReadPolicy = "Permission:" + UsersRead;
}