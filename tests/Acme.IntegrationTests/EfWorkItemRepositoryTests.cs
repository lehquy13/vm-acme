using Microsoft.EntityFrameworkCore;
using Acme.Infrastructure.Persistence;

namespace Acme.IntegrationTests;

public sealed class EfWorkItemRepositoryTests
{
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void List_order_can_be_translated_by_database_provider(string provider)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AcmeDbContext>();
        if (provider == "PostgreSql")
        {
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=Acme;Username=test;Password=test",
                database => database.MigrationsAssembly("Acme.Migrations.PostgreSql"));
        }
        else
        {
            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=Acme;User Id=test;Password=test;TrustServerCertificate=true",
                database => database.MigrationsAssembly("Acme.Migrations.SqlServer"));
        }

        var options = optionsBuilder.Options;
        using var dbContext = new AcmeDbContext(options);

        var query = dbContext.WorkItems.OrderBy(workItem => workItem.Title);

        var sql = query.ToQueryString();
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }
}
