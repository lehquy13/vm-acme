using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VmAcme.Persistence;

namespace VmAcme;

public sealed class MigrationModelTests
{
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Migration_snapshot_matches_current_context_model(string provider)
    {
        var options = new DbContextOptionsBuilder<VmAcmeDbContext>();
        if (provider == "PostgreSql")
        {
            options.UseNpgsql(
                "Host=localhost;Database=VmAcme;Username=test;Password=test",
                database => database.MigrationsAssembly("VmAcme.Migrations.PostgreSql"));
        }
        else
        {
            options.UseSqlServer(
                "Server=localhost;Database=VmAcme;User Id=test;Password=test;TrustServerCertificate=true",
                database => database.MigrationsAssembly("VmAcme.Migrations.SqlServer"));
        }

        using var dbContext = new VmAcmeDbContext(options.Options);

        Assert.False(dbContext.Database.HasPendingModelChanges());
        var migrations = dbContext.GetService<IMigrationsAssembly>();
        foreach (var migrationId in migrations.Migrations.Keys)
        {
            Assert.Equal(migrationId, migrations.FindMigrationId(migrationId));
        }

        var script = dbContext.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WorkItems", script, StringComparison.Ordinal);
        Assert.Contains("TodoItems", script, StringComparison.Ordinal);
        Assert.Contains("Users", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.OrdinalIgnoreCase);
    }
}