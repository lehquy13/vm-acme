using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Template.Infrastructure.Persistence;

namespace Template.IntegrationTests;

public sealed class MigrationModelTests
{
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Migration_snapshot_matches_current_context_model(string provider)
    {
        var options = new DbContextOptionsBuilder<TemplateDbContext>();
        if (provider == "PostgreSql")
        {
            options.UseNpgsql(
                "Host=localhost;Database=Template;Username=test;Password=test",
                database => database.MigrationsAssembly("Template.Migrations.PostgreSql"));
        }
        else
        {
            options.UseSqlServer(
                "Server=localhost;Database=Template;User Id=test;Password=test;TrustServerCertificate=true",
                database => database.MigrationsAssembly("Template.Migrations.SqlServer"));
        }

        using var dbContext = new TemplateDbContext(options.Options);

        Assert.False(dbContext.Database.HasPendingModelChanges());

        var script = dbContext.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WorkItems", script, StringComparison.Ordinal);
    }
}
