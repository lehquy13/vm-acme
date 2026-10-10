using Microsoft.EntityFrameworkCore;
using Template.Infrastructure.Persistence;

namespace Template.IntegrationTests;

public sealed class EfWorkItemRepositoryTests
{
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void List_order_can_be_translated_by_database_provider(string provider)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TemplateDbContext>();
        if (provider == "PostgreSql")
        {
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=Template;Username=test;Password=test",
                database => database.MigrationsAssembly("Template.Migrations.PostgreSql"));
        }
        else
        {
            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=Template;User Id=test;Password=test;TrustServerCertificate=true",
                database => database.MigrationsAssembly("Template.Migrations.SqlServer"));
        }

        var options = optionsBuilder.Options;
        using var dbContext = new TemplateDbContext(options);

        var query = dbContext.WorkItems.OrderBy(workItem => workItem.Title);

        var sql = query.ToQueryString();
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }
}
