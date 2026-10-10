using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Acme.Infrastructure.Persistence;

namespace Acme.IntegrationTests;

public sealed class MigrationModelTests
{
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Migration_snapshot_matches_current_context_model(string provider)
    {
        var options = new DbContextOptionsBuilder<AcmeDbContext>();
        if (provider == "PostgreSql")
        {
            options.UseNpgsql(
                "Host=localhost;Database=Acme;Username=test;Password=test",
                database => database.MigrationsAssembly("Acme.Migrations.PostgreSql"));
        }
        else
        {
            options.UseSqlServer(
                "Server=localhost;Database=Acme;User Id=test;Password=test;TrustServerCertificate=true",
                database => database.MigrationsAssembly("Acme.Migrations.SqlServer"));
        }

        using var dbContext = new AcmeDbContext(options.Options);

        Assert.False(dbContext.Database.HasPendingModelChanges());

        var script = dbContext.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WorkItems", script, StringComparison.Ordinal);
    }
}
