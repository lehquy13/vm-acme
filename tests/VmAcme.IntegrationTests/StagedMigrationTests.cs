using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VmAcme.Persistence;
using VmAcme.WorkItems;

namespace VmAcme;

public sealed class StagedMigrationTests
{
    [LiveDatabaseTheory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task V1_todo_schema_then_V2_legacy_revision_preserves_rows(string provider)
    {
        // A fresh, uniquely named database inside the disposable test container.
        var databaseName = "VmAcmeStage_" + Guid.NewGuid().ToString("N");
        var connection = Environment.GetEnvironmentVariable($"VM_ACME_TEST_{provider.ToUpperInvariant()}")!;
        var builder = new DbContextOptionsBuilder<VmAcmeDbContext>();
        var assembly = "VmAcme.Migrations." + provider;
        if (provider == "PostgreSql")
        {
            var settings = new NpgsqlConnectionStringBuilder(connection) { Database = databaseName };
            builder.UseNpgsql(settings.ConnectionString, x => x.MigrationsAssembly(assembly));
        }
        else
        {
            var settings = new SqlConnectionStringBuilder(connection) { InitialCatalog = databaseName };
            builder.UseSqlServer(settings.ConnectionString, x => x.MigrationsAssembly(assembly));
        }

        var v2Options = builder.Options;
        builder.ReplaceService<IMigrationsAssembly, V1MigrationsAssembly>();
        var item = WorkItem.Create(new WorkItemTitle("Legacy revision fixture"));
        await using (var v1 = new VmAcmeDbContext(builder.Options))
        {
            await v1.Database.MigrateAsync();
            Assert.DoesNotContain("20261011000100_AddDeploymentRevision",
                await v1.Database.GetAppliedMigrationsAsync());
            await new EfWorkItemRepository(v1).AddAsync(item, CancellationToken.None);
            Assert.Empty(await v1.TodoItems.ToArrayAsync());
            Assert.Empty(await v1.Users.ToArrayAsync());
        }

        await using (var v2 = new VmAcmeDbContext(v2Options))
        {
            // V2 uses the latest target, so it applies the revision missing from the V1 artifact.
            await v2.Database.MigrateAsync();
            Assert.Empty(await v2.Database.GetPendingMigrationsAsync());
            var persisted = await new EfWorkItemRepository(v2).GetAsync(item.Id, CancellationToken.None);
            Assert.Equal("Legacy revision fixture", persisted!.Title.Value);
            var sql = provider == "PostgreSql"
                ? "SELECT \"DeploymentRevision\" AS \"Value\" FROM \"WorkItems\" WHERE \"Id\" = {0}"
                : "SELECT DeploymentRevision AS Value FROM WorkItems WHERE Id = {0}";
            Assert.Equal("v2", await v2.Database.SqlQueryRaw<string>(sql, item.Id.Value).SingleAsync());
        }
    }
}

// Mirrors MigrationRelease=1 without rebuilding test artifacts mid-run.
public sealed class V1MigrationsAssembly : IMigrationsAssembly
{
    public V1MigrationsAssembly(IDbContextOptions options)
    {
        var name = options.Extensions.OfType<RelationalOptionsExtension>().Single().MigrationsAssembly!;
        Assembly = Assembly.Load(name);
        Migrations = new SortedDictionary<string, TypeInfo>(Assembly.DefinedTypes
            .Where(x => typeof(Migration).IsAssignableFrom(x) &&
                        x.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(VmAcmeDbContext))
            .Where(x => x.GetCustomAttribute<MigrationAttribute>()!.Id != "20261011000100_AddDeploymentRevision")
            .ToDictionary(x => x.GetCustomAttribute<MigrationAttribute>()!.Id, x => x), StringComparer.Ordinal);
        var snapshot = Assembly.DefinedTypes.Single(x =>
            typeof(ModelSnapshot).IsAssignableFrom(x) &&
            x.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(VmAcmeDbContext));
        ModelSnapshot = (ModelSnapshot)Activator.CreateInstance(snapshot.AsType())!;
    }

    public Assembly Assembly { get; }
    public IReadOnlyDictionary<string, TypeInfo> Migrations { get; }
    public ModelSnapshot? ModelSnapshot { get; }

    public string? FindMigrationId(string nameOrId) => Migrations.Keys.FirstOrDefault(x =>
        x.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
        x[15..].Equals(nameOrId, StringComparison.OrdinalIgnoreCase));

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        var migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
        migration.ActiveProvider = activeProvider;
        return migration;
    }
}