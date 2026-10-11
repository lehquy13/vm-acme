using Microsoft.EntityFrameworkCore;
using VmAcme.Persistence;

namespace VmAcme;

public sealed class EfWorkItemRepositoryTests
{
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void List_order_can_be_translated_by_database_provider(string provider)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VmAcmeDbContext>();
        if (provider == "PostgreSql")
        {
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=VmAcme;Username=test;Password=test",
                database => database.MigrationsAssembly("VmAcme.Migrations.PostgreSql"));
        }
        else
        {
            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=VmAcme;User Id=test;Password=test;TrustServerCertificate=true",
                database => database.MigrationsAssembly("VmAcme.Migrations.SqlServer"));
        }

        var options = optionsBuilder.Options;
        using var dbContext = new VmAcmeDbContext(options);

        var query = dbContext.WorkItems.OrderBy(workItem => workItem.Title);

        var sql = query.ToQueryString();
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }
}