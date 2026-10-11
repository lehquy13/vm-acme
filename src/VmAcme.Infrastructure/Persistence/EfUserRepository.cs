using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VmAcme.Common;
using VmAcme.Users;

namespace VmAcme.Persistence;

public sealed class EfUserRepository(VmAcmeDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

    public async Task<Result<Unit>> AddAsync(User user, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == user.NormalizedEmail, cancellationToken))
            return UserPersistenceErrors.DuplicateEmail();
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Success(new Unit());
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
                or SqlException { Number: 2601 or 2627 })
        {
            db.Entry(user).State = EntityState.Detached;
            return UserPersistenceErrors.DuplicateEmail();
        }
    }
}