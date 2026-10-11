using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VmAcme.Persistence;

namespace VmAcme;

// Registered only when both POC_MODE and POC_FAIL_MIGRATION are explicitly enabled.
// Normal releases use EF's standard provider migration assembly unchanged.
public sealed class PocFailureMigrationsAssembly : IMigrationsAssembly
{
    public PocFailureMigrationsAssembly(IDbContextOptions options)
    {
        var name = options.Extensions.OfType<RelationalOptionsExtension>().Single().MigrationsAssembly
                   ?? throw new InvalidOperationException("Provider migration assembly is required.");
        Assembly = Assembly.Load(name);
        var migrations = Assembly.DefinedTypes
            .Where(type => typeof(Migration).IsAssignableFrom(type) &&
                           type.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(VmAcmeDbContext))
            .ToDictionary(type => type.GetCustomAttribute<MigrationAttribute>()!.Id, type => type);
        migrations.Add(PocFailureMigration.Id, typeof(PocFailureMigration).GetTypeInfo());
        Migrations = new SortedDictionary<string, TypeInfo>(migrations, StringComparer.Ordinal);
        var snapshotType = Assembly.DefinedTypes.Single(type =>
            typeof(ModelSnapshot).IsAssignableFrom(type) &&
            type.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(VmAcmeDbContext));
        ModelSnapshot = (ModelSnapshot)Activator.CreateInstance(snapshotType.AsType())!;
    }

    public IReadOnlyDictionary<string, TypeInfo> Migrations { get; }
    public ModelSnapshot? ModelSnapshot { get; }
    public Assembly Assembly { get; }

    public string? FindMigrationId(string nameOrId) => Migrations.Keys.FirstOrDefault(id =>
        string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id[15..], nameOrId, StringComparison.OrdinalIgnoreCase));

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        var migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
        migration.ActiveProvider = activeProvider;
        return migration;
    }
}

[DbContext(typeof(VmAcmeDbContext))]
[Migration(Id)]
public sealed class PocFailureMigration : Migration
{
    public const string Id = "20261012000100_PocFailure";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE TABLE VmAcmePocRollbackProbe (Id int NOT NULL)");
        migrationBuilder.Sql("SELECT * FROM VmAcmeDeliberatelyMissingTable");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE VmAcmePocRollbackProbe");
}