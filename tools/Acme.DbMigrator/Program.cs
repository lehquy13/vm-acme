using Microsoft.EntityFrameworkCore;
using Acme.Infrastructure.Persistence;

var options = ParseArguments(args);
if (options is null)
{
    return 2;
}

var dbOptions = new DbContextOptionsBuilder<AcmeDbContext>();
switch (options.Value.Provider)
{
    case "PostgreSql":
        _ = typeof(Acme.Migrations.PostgreSql.InitialCreate).Assembly;
        dbOptions.UseNpgsql(options.Value.ConnectionString, provider =>
            provider.MigrationsAssembly("Acme.Migrations.PostgreSql"));
        break;
    case "SqlServer":
        _ = typeof(Acme.Migrations.SqlServer.InitialCreate).Assembly;
        dbOptions.UseSqlServer(options.Value.ConnectionString, provider =>
            provider.MigrationsAssembly("Acme.Migrations.SqlServer"));
        break;
}

await using (var context = new AcmeDbContext(dbOptions.Options))
{
    await context.Database.MigrateAsync();
}

return 0;

static MigrationOptions? ParseArguments(string[] arguments)
{
    string? provider = null;
    string? connectionString = null;

    for (var index = 0; index < arguments.Length; index++)
    {
        if (index + 1 >= arguments.Length)
        {
            break;
        }

        switch (arguments[index])
        {
            case "--provider":
                provider = arguments[++index];
                break;
            case "--connection":
                connectionString = arguments[++index];
                break;
        }
    }

    if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(connectionString))
    {
        return new MigrationOptions("PostgreSql", connectionString);
    }

    if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(connectionString))
    {
        return new MigrationOptions("SqlServer", connectionString);
    }

    Console.Error.WriteLine("Usage: Acme.DbMigrator --provider PostgreSql|SqlServer --connection <connection-string>");
    return null;
}

readonly record struct MigrationOptions(string Provider, string ConnectionString);
