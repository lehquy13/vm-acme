using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using VmAcme.Configuration;
using VmAcme.Persistence;

namespace VmAcme;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VmAcmeDbContext>
{
    public VmAcmeDbContext CreateDbContext(string[] args)
    {
        var configuration = SecretBootstrap.CreateConfiguration();
        configuration["Persistence:Provider"] = "SqlServer";
        SecretBootstrap.LoadAsync(configuration, includeJwt: false).GetAwaiter().GetResult();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection before creating the SQL Server design-time context.");

        var options = new DbContextOptionsBuilder<VmAcmeDbContext>()
            .UseSqlServer(connectionString, builder => builder.MigrationsAssembly("VmAcme.Migrations.SqlServer"))
            .Options;
        return new VmAcmeDbContext(options);
    }
}
