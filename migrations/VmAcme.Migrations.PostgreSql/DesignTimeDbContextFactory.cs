using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VmAcme.Persistence;

namespace VmAcme;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VmAcmeDbContext>
{
    public VmAcmeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection before creating the PostgreSQL design-time context.");

        var options = new DbContextOptionsBuilder<VmAcmeDbContext>()
            .UseNpgsql(connectionString, builder => builder.MigrationsAssembly("VmAcme.Migrations.PostgreSql"))
            .Options;
        return new VmAcmeDbContext(options);
    }
}