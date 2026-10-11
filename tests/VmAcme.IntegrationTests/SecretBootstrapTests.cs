using Microsoft.Extensions.Configuration;
using VmAcme.Configuration;

namespace VmAcme;

public sealed class SecretBootstrapTests
{
    private static ConfigurationManager Settings() => new()
    {
        ["Secrets:Mode"] = "KeyVaultRequired",
        ["Secrets:VaultUri"] = "https://example.vault.azure.net/",
        ["Secrets:ConnectionSecret"] = "api-connection",
        ["Secrets:JwtSecret"] = "api-jwt",
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fallback"
    };

    [Fact]
    public async Task Api_reads_only_named_secrets_and_overrides_environment()
    {
        var configuration = Settings();
        var names = new List<string>();
        await SecretBootstrap.LoadAsync(configuration, true, (_, name, _) =>
        {
            names.Add(name);
            return Task.FromResult(name == "api-jwt" ? new string('k', 40) : "Host=localhost;Database=vault");
        });
        Assert.Equal(new[] { "api-connection", "api-jwt" }, names);
        Assert.Equal("Host=localhost;Database=vault", configuration.GetConnectionString("DefaultConnection"));
    }

    [Fact]
    public async Task Migrator_does_not_read_jwt()
    {
        var configuration = Settings();
        configuration["Secrets:JwtSecret"] = null;
        await SecretBootstrap.LoadAsync(configuration, false, (_, name, _) =>
        {
            Assert.Equal("api-connection", name);
            return Task.FromResult("Host=localhost;Database=vault");
        });
    }

    [Fact]
    public async Task Read_failure_aborts_without_partial_configuration()
    {
        var configuration = Settings();
        await Assert.ThrowsAsync<InvalidOperationException>(() => SecretBootstrap.LoadAsync(configuration, true,
            (_, name, _) => name == "api-jwt" ? throw new IOException("Unavailable") : Task.FromResult("new")));
        Assert.Equal("Host=localhost;Database=fallback", configuration.GetConnectionString("DefaultConnection"));
    }

    [Theory]
    [InlineData("http://example.vault.azure.net/")]
    [InlineData("https://example.vault.azure.net/secrets/")]
    public async Task Invalid_vault_is_rejected_before_reads(string uri)
    {
        var configuration = Settings();
        configuration["Secrets:VaultUri"] = uri;
        await Assert.ThrowsAsync<InvalidOperationException>(() => SecretBootstrap.LoadAsync(configuration, true,
            (_, _, _) => throw new Exception("must not read")));
    }
}
