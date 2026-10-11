using Microsoft.EntityFrameworkCore;
using VmAcme.Authentication;
using VmAcme.Common;
using VmAcme.Persistence;
using VmAcme.Todos;
using VmAcme.Users;

namespace VmAcme;

// Run scripts/test-databases.ps1 to supply disposable PostgreSQL and SQL Server instances.
public sealed class LiveDatabaseTests
{
    [LiveDatabaseTheory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task Migrations_and_owner_scoped_repository_lifecycle(string provider)
    {
        var options = Options(provider);
        var user = User.Create($"{Guid.NewGuid()}@example.com", "Database", "Test",
            new PasswordHasher().Hash("Strong-password-123!"));
        var id = Guid.Empty;
        var clock = DateTimeOffset.UtcNow;
        var now = new DateTimeOffset(clock.Ticks - clock.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
        await using (var db = new VmAcmeDbContext(options))
        {
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var users = new EfUserRepository(db);
            Assert.True((await users.AddAsync(user, CancellationToken.None)).IsSuccess);
            var duplicate = User.Create(user.Email.ToUpperInvariant(), "Other", "User", "hash");
            Assert.Equal(ErrorType.Conflict,
                (await users.AddAsync(duplicate, CancellationToken.None)).Error!.Type);
            var todo = TodoItem.Create(user.Id, "Database round trip", Priority.Top, now.AddDays(1),
                ["test", "database"], now);
            id = todo.Id;
            await new EfTodoRepository(db).AddAsync(todo, CancellationToken.None);
        }

        await using (var db = new VmAcmeDbContext(options))
        {
            var repository = new EfTodoRepository(db);
            Assert.Null(await repository.GetAsync(id, Guid.NewGuid(), CancellationToken.None));
            Assert.Empty(await repository.ListAsync(Guid.NewGuid(), CancellationToken.None));
            var todo = await repository.GetAsync(id, user.Id, CancellationToken.None);
            Assert.NotNull(todo);
            Assert.Equal(new[] { "test", "database" }, todo.Labels);
            Assert.Equal(Priority.Top, todo.Priority);
            Assert.Equal(now.UtcDateTime, todo.CreatedAt.UtcDateTime);
            Assert.Single(await repository.ListAsync(user.Id, CancellationToken.None));
            Assert.True(todo.Complete(now.AddHours(1)).IsSuccess);
            Assert.True((await repository.UpdateAsync(todo, CancellationToken.None)).IsSuccess);
        }

        await using (var db = new VmAcmeDbContext(options))
        {
            var repository = new EfTodoRepository(db);
            var todo = await repository.GetAsync(id, user.Id, CancellationToken.None);
            Assert.NotNull(todo);
            Assert.True(todo.IsCompleted);
            Assert.Equal(now.AddHours(1).UtcDateTime, todo.CompletedAt!.Value.UtcDateTime);
            Assert.True((await repository.DeleteAsync(todo, CancellationToken.None)).IsSuccess);
            Assert.Null(await repository.GetAsync(id, user.Id, CancellationToken.None));
        }
    }

    [LiveDatabaseTheory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task Concurrent_completion_returns_conflict(string provider)
    {
        var options = Options(provider);
        var user = User.Create($"{Guid.NewGuid()}@example.com", "Concurrency", "Test", "hash");
        var todo = TodoItem.Create(user.Id, "Complete once", Priority.Normal, null, [], DateTimeOffset.UtcNow);
        await using (var db = new VmAcmeDbContext(options))
        {
            await db.Database.MigrateAsync();
            await new EfUserRepository(db).AddAsync(user, CancellationToken.None);
            await new EfTodoRepository(db).AddAsync(todo, CancellationToken.None);
        }

        await using var first = new VmAcmeDbContext(options);
        await using var second = new VmAcmeDbContext(options);
        var firstRepository = new EfTodoRepository(first);
        var secondRepository = new EfTodoRepository(second);
        var a = (await firstRepository.GetAsync(todo.Id, user.Id, CancellationToken.None))!;
        var b = (await secondRepository.GetAsync(todo.Id, user.Id, CancellationToken.None))!;
        a.Complete(DateTimeOffset.UtcNow);
        b.Complete(DateTimeOffset.UtcNow);
        Assert.True((await firstRepository.UpdateAsync(a, CancellationToken.None)).IsSuccess);
        Assert.Equal(ErrorType.Conflict,
            (await secondRepository.UpdateAsync(b, CancellationToken.None)).Error!.Type);
    }

    [LiveDatabaseTheory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task Concurrent_registration_keeps_email_unique(string provider)
    {
        var options = Options(provider);
        var email = $"{Guid.NewGuid()}@example.com";
        await using var first = new VmAcmeDbContext(options);
        await first.Database.MigrateAsync();
        await using var second = new VmAcmeDbContext(options);
        var results = await Task.WhenAll(
            new EfUserRepository(first).AddAsync(User.Create(email, "First", "User", "hash"), CancellationToken.None),
            new EfUserRepository(second).AddAsync(User.Create(email.ToUpperInvariant(), "Second", "User", "hash"),
                CancellationToken.None));
        Assert.Single(results, x => x.IsSuccess);
        Assert.Equal(ErrorType.Conflict, Assert.Single(results, x => x.IsFailure).Error!.Type);
        Assert.Equal(1, await first.Users.CountAsync(x => x.NormalizedEmail == User.NormalizeEmail(email)));
    }

    private static DbContextOptions<VmAcmeDbContext> Options(string provider)
    {
        var builder = new DbContextOptionsBuilder<VmAcmeDbContext>();
        var connection = Environment.GetEnvironmentVariable($"VM_ACME_TEST_{provider.ToUpperInvariant()}")!;
        if (provider == "PostgreSql")
            builder.UseNpgsql(connection, x => x.MigrationsAssembly("VmAcme.Migrations.PostgreSql"));
        else builder.UseSqlServer(connection, x => x.MigrationsAssembly("VmAcme.Migrations.SqlServer"));
        return builder.Options;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveDatabaseTheoryAttribute : TheoryAttribute
{
    public LiveDatabaseTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VM_ACME_TEST_POSTGRESQL")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VM_ACME_TEST_SQLSERVER")))
            Skip = "Run scripts/test-databases.ps1 to test disposable PostgreSQL and SQL Server databases.";
    }
}