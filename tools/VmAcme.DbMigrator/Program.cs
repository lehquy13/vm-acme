using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VmAcme;
using VmAcme.Migrations.PostgreSql;
using VmAcme.Persistence;

MigrationOptions? options;
try
{
    options = ParseArguments(args);
}
catch (Exception exception)
{
    await Console.Error.WriteLineAsync($"Invalid migration configuration ({exception.GetType().Name}).");
    return 2;
}

if (options is null)
{
    return 2;
}

var migrationOptions = options.Value;

using var timeout = new CancellationTokenSource(migrationOptions.Timeout);

try
{
    var dbOptions = new DbContextOptionsBuilder<VmAcmeDbContext>();
    switch (migrationOptions.Provider)
    {
        case "PostgreSql":
            _ = typeof(InitialCreate).Assembly;
            dbOptions.UseNpgsql(migrationOptions.ConnectionString, provider =>
                provider.MigrationsAssembly("VmAcme.Migrations.PostgreSql"));
            break;
        case "SqlServer":
            _ = typeof(VmAcme.Migrations.SqlServer.InitialCreate).Assembly;
            dbOptions.UseSqlServer(migrationOptions.ConnectionString, provider =>
                provider.MigrationsAssembly("VmAcme.Migrations.SqlServer"));
            break;
    }

    if (migrationOptions.FailMigration)
    {
        dbOptions.ReplaceService<IMigrationsAssembly, PocFailureMigrationsAssembly>();
        migrationOptions = migrationOptions with { TargetMigration = PocFailureMigration.Id };
    }

    await using var context = new VmAcmeDbContext(dbOptions.Options);

    var appliedBefore = (await context.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray();
    var pendingBefore = (await context.Database.GetPendingMigrationsAsync(timeout.Token)).ToArray();
    Console.WriteLine($"MIGRATIONS_BEFORE={appliedBefore.Length} PENDING={pendingBefore.Length}");

    var migrator = context.Database.GetService<IMigrator>();
    if (migrationOptions.TargetMigration is not null)
    {
        var allMigrationIds = context.Database.GetService<IMigrationsAssembly>().Migrations.Keys.ToArray();
        var targetIndex = Array.IndexOf(allMigrationIds, migrationOptions.TargetMigration);
        if (targetIndex < 0)
        {
            throw new InvalidOperationException("Requested migration target is not registered.");
        }

        var appliedIndexes = appliedBefore
            .Select(id => Array.IndexOf(allMigrationIds, id))
            .Where(index => index >= 0)
            .ToArray();

        if (appliedBefore.Contains(migrationOptions.TargetMigration, StringComparer.Ordinal) ||
            appliedIndexes.Any(index => index > targetIndex))
        {
            Console.WriteLine($"MIGRATION_TARGET_SKIPPED={migrationOptions.TargetMigration}");
        }
        else
        {
            await migrator.MigrateAsync(migrationOptions.TargetMigration, timeout.Token);
        }
    }
    else
    {
        await migrator.MigrateAsync(null, timeout.Token);
    }

    var appliedAfter = (await context.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray();
    var appliedThisRun = appliedAfter
        .Except(appliedBefore, StringComparer.Ordinal)
        .ToArray();
    foreach (var migrationId in appliedThisRun)
    {
        Console.WriteLine($"APPLIED_MIGRATION={migrationId}");
    }

    Console.WriteLine($"MIGRATIONS_AFTER={appliedAfter.Length} APPLIED_THIS_RUN={appliedThisRun.Length}");
    return 0;
}
catch (Exception exception)
{
    await Console.Error.WriteLineAsync($"Migration failed ({exception.GetType().Name}).");
    return 1;
}

static MigrationOptions? ParseArguments(string[] arguments)
{
    var (cliProvider, cliConnectionString) = ParseCommandLineArguments(arguments);

    var provider = cliProvider ?? Environment.GetEnvironmentVariable("Persistence__Provider");
    var connectionString = cliConnectionString ??
                           Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    if (!TryNormalizePersistenceSettings(provider, connectionString, out var normalizedProvider,
            out var normalizedConnectionString))
    {
        Console.Error.WriteLine(
            "Set Persistence__Provider and ConnectionStrings__DefaultConnection, or use --provider and --connection.");
        return null;
    }

    var failMigration = IsEnvironmentFlagEnabled("POC_FAIL_MIGRATION");
    var pocMode = IsEnvironmentFlagEnabled("POC_MODE");
    if (failMigration && !pocMode)
    {
        Console.Error.WriteLine("POC_FAIL_MIGRATION requires POC_MODE=true.");
        return null;
    }

    var timeoutSeconds = GetMigrationTimeoutSeconds();
    if (timeoutSeconds <= 0)
    {
        Console.Error.WriteLine("MIGRATION_TIMEOUT_SECONDS must be a positive integer.");
        return null;
    }

    return new MigrationOptions(
        normalizedProvider,
        normalizedConnectionString,
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MIGRATION_TARGET"))
            ? null
            : Environment.GetEnvironmentVariable("MIGRATION_TARGET"),
        failMigration,
        TimeSpan.FromSeconds(timeoutSeconds));
}

static (string? Provider, string? ConnectionString) ParseCommandLineArguments(string[] arguments)
{
    string? provider = null;
    string? connectionString = null;

    using var enumerator = ((IEnumerable<string>)arguments).GetEnumerator();
    while (enumerator.MoveNext())
    {
        switch (enumerator.Current)
        {
            case "--provider":
                if (enumerator.MoveNext())
                {
                    provider = enumerator.Current;
                }

                break;
            case "--connection":
                if (enumerator.MoveNext())
                {
                    connectionString = enumerator.Current;
                }

                break;
        }
    }

    return (provider, connectionString);
}

static bool TryNormalizePersistenceSettings(
    string? provider,
    string? connectionString,
    out string normalizedProvider,
    out string normalizedConnectionString)
{
    if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(connectionString))
    {
        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        connectionBuilder.Timeout = connectionBuilder.Timeout > 0 ? connectionBuilder.Timeout : 15;
        normalizedProvider = "PostgreSql";
        normalizedConnectionString = connectionBuilder.ConnectionString;
        return true;
    }

    if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(connectionString))
    {
        var connectionBuilder = new SqlConnectionStringBuilder(connectionString);
        connectionBuilder.ConnectTimeout = connectionBuilder.ConnectTimeout > 0
            ? connectionBuilder.ConnectTimeout
            : 15;
        normalizedProvider = "SqlServer";
        normalizedConnectionString = connectionBuilder.ConnectionString;
        return true;
    }

    normalizedProvider = string.Empty;
    normalizedConnectionString = string.Empty;
    return false;
}

static bool IsEnvironmentFlagEnabled(string name) =>
    string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase);

static int GetMigrationTimeoutSeconds()
{
    return int.TryParse(Environment.GetEnvironmentVariable("MIGRATION_TIMEOUT_SECONDS"), out var timeoutSeconds)
        ? timeoutSeconds
        : 180;
}

namespace VmAcme
{
    readonly record struct MigrationOptions(
        string Provider,
        string ConnectionString,
        string? TargetMigration,
        bool FailMigration,
        TimeSpan Timeout);
}