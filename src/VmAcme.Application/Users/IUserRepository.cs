using VmAcme.Common;

namespace VmAcme.Users;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<Result<Unit>> AddAsync(User user, CancellationToken cancellationToken);
}