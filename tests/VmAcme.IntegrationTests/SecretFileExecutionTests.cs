using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VmAcme;

[CollectionDefinition("Operator secret file", DisableParallelization = true)]
public sealed class OperatorSecretFileCollection;

[Collection("Operator secret file")]
public sealed class SecretFileExecutionTests
{
    [Fact]
    public async Task Api_and_both_bundle_factories_load_operator_file_and_environment_overrides_it()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vm-acme-path-proof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var variables = new[] { "Secrets__Mode", "Secrets__FallbackFile", "Secrets__BaseDirectory", "ConnectionStrings__DefaultConnection", "Authentication__Jwt__SigningKey" };
        var previous = variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            var file = Path.Combine(directory, "appsettings.secrets.json");
            var key = new string('f', 40);
            await File.WriteAllTextAsync(file, System.Text.Json.JsonSerializer.Serialize(new {
                ConnectionStrings = new { DefaultConnection = "Host=localhost;Database=file_db" },
                Authentication = new { Jwt = new { SigningKey = key } }
            }));
            Environment.SetEnvironmentVariable("Secrets__Mode", "AppSettings");
            Environment.SetEnvironmentVariable("Secrets__FallbackFile", "appsettings.secrets.json");
            Environment.SetEnvironmentVariable("Secrets__BaseDirectory", directory);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
            Environment.SetEnvironmentVariable("Authentication__Jwt__SigningKey", null);
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development")))
            {
                using var client = factory.CreateClient();
                Assert.True((await client.GetAsync("/health")).IsSuccessStatusCode);
                Assert.Equal(key, factory.Services.GetRequiredService<IConfiguration>()["Authentication:Jwt:SigningKey"]);
            }
            using (var db = CreateBundleContext("PostgreSql"))
                Assert.Contains("file_db", db.Database.GetConnectionString());
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=localhost;Database=environment_db;Integrated Security=True");
            using (var db = CreateBundleContext("SqlServer"))
                Assert.Contains("environment_db", db.Database.GetConnectionString());
        }
        finally
        {
            foreach (var name in variables) Environment.SetEnvironmentVariable(name, previous[name]);
            File.Delete(Path.Combine(directory, "appsettings.secrets.json"));
            Directory.Delete(directory);
        }
    }

    private static DbContext CreateBundleContext(string provider)
    {
        var type = System.Reflection.Assembly.Load($"VmAcme.Migrations.{provider}").GetType("VmAcme.DesignTimeDbContextFactory", throwOnError: true)!;
        return (DbContext)type.GetMethod("CreateDbContext")!.Invoke(Activator.CreateInstance(type), [Array.Empty<string>()])!;
    }
}
