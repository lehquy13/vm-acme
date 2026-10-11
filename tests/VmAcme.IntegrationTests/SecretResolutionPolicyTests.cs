using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using VmAcme.Configuration;

namespace VmAcme;

public sealed class SecretResolutionPolicyTests
{
    private const string Connection = "Host=localhost;Database=test;Username=test;Password=sentinel-password";
    private const string Signing = "sentinel-signing-key-over-thirty-two-characters";
    private static ConfigurationManager Settings(string mode) => new()
    {
        ["Secrets:Mode"] = mode, ["Secrets:VaultUri"] = "https://example.vault.azure.net/",
        ["Secrets:ConnectionSecret"] = "connection", ["Secrets:JwtSecret"] = "jwt",
        ["Persistence:Provider"] = "PostgreSql", ["ConnectionStrings:DefaultConnection"] = Connection,
        ["Authentication:Jwt:SigningKey"] = Signing
    };

    [Theory]
    [InlineData("outage")]
    [InlineData("denied")]
    [InlineData("missing")]
    [InlineData("empty")]
    public async Task Fallback_is_atomic_sanitized_and_observable(string failure)
    {
        var config = Settings("KeyVaultWithFallback");
        var warnings = new List<string>();
        var measurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) => { if (instrument.Meter.Name == "VmAcme.Secrets") meter.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => { lock (measurements) measurements.Add(value); foreach (var tag in tags) Assert.DoesNotContain("sentinel", tag.Value?.ToString() ?? ""); });
        listener.Start();
        await SecretBootstrap.LoadAsync(config, true, (_, name, _) => name == "connection"
            ? Task.FromResult(Connection + ";Application Name=partial-vault")
            : failure == "empty" ? Task.FromResult("") : failure == "denied" ? throw new Azure.RequestFailedException(403, "sentinel-denied-payload")
            : failure == "missing" ? throw new Azure.RequestFailedException(404, "sentinel-missing-payload") : throw new IOException("sentinel-secret-service-payload"), warning: warnings.Add);
        Assert.Equal(Connection, config.GetConnectionString("DefaultConnection"));
        Assert.Equal(Signing, config["Authentication:Jwt:SigningKey"]);
        Assert.Equal("Fallback", config["Secrets:ResolvedSource"]);
        Assert.Single(warnings);
        Assert.DoesNotContain("sentinel", warnings[0]);
        Assert.Contains(1L, measurements);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task Incomplete_fallback_aborts_without_publishing(string signing)
    {
        var config = Settings("KeyVaultWithFallback");
        config["Authentication:Jwt:SigningKey"] = signing;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SecretBootstrap.LoadAsync(config, true, (_, _, _) => throw new IOException("sentinel")));
        Assert.Null(config["Secrets:ResolvedSource"]);
        Assert.DoesNotContain("sentinel", error.ToString());
    }

    [Fact]
    public async Task Vault_success_does_not_load_unavailable_fallback_and_overrides_settings()
    {
        var config = Settings("KeyVaultWithFallback");
        config["Secrets:FallbackFile"] = "missing.secrets.json";
        await SecretBootstrap.LoadAsync(config, true, (_, name, _) => Task.FromResult(name == "jwt" ? Signing + "vault" : Connection + ";Application Name=vault"));
        Assert.EndsWith("vault", config.GetConnectionString("DefaultConnection"));
        Assert.Equal("KeyVault", config["Secrets:ResolvedSource"]);
    }

    [Fact]
    public async Task Appsettings_never_contacts_vault()
    {
        var config = Settings("AppSettings");
        await SecretBootstrap.LoadAsync(config, true, (_, _, _) => throw new Exception("must not read"));
        Assert.Equal("AppSettings", config["Secrets:ResolvedSource"]);
    }

    [Fact]
    public async Task Missing_workload_identity_configuration_can_activate_explicit_fallback()
    {
        var config = Settings("KeyVaultWithFallback");
        var warnings = new List<string>();
        await SecretBootstrap.LoadAsync(config, true, warning: warnings.Add);
        Assert.Equal("Fallback", config["Secrets:ResolvedSource"]);
        Assert.Single(warnings);
        Assert.DoesNotContain("sentinel", warnings[0]);
    }

    [Theory]
    [InlineData("denied")]
    [InlineData("missing")]
    [InlineData("empty")]
    public async Task Required_mode_never_falls_back(string failure)
    {
        var config = Settings("KeyVaultRequired");
        var warnings = new List<string>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SecretBootstrap.LoadAsync(config, true,
            (_, _, _) => failure == "empty" ? Task.FromResult("") : throw new Azure.RequestFailedException(failure == "denied" ? 403 : 404, "sentinel-payload"), warning: warnings.Add));
        Assert.Null(config["Secrets:ResolvedSource"]);
        Assert.Empty(warnings);
        Assert.DoesNotContain("sentinel", error.ToString());
    }

    [Theory]
    [InlineData("KeyVaultRequired")]
    [InlineData("KeyVaultWithFallback")]
    public async Task Migration_requests_only_connection_and_does_not_publish_signing_key(string mode)
    {
        var config = Settings(mode);
        config["Authentication:Jwt:SigningKey"] = null;
        config["Secrets:JwtSecret"] = null;
        var names = new List<string>();
        await SecretBootstrap.LoadAsync(config, false, (_, name, _) => { names.Add(name); return Task.FromResult(Connection); });
        Assert.Equal(new[] { "connection" }, names);
        Assert.Null(config["Authentication:Jwt:SigningKey"]);
        if (mode == "KeyVaultWithFallback")
        {
            await SecretBootstrap.LoadAsync(config, false, (_, _, _) => throw new IOException());
            Assert.Equal("Fallback", config["Secrets:ResolvedSource"]);
            Assert.Null(config["Authentication:Jwt:SigningKey"]);
        }
    }

    [Fact]
    public async Task Missing_explicit_file_and_invalid_connection_fail_without_publication()
    {
        var config = Settings("AppSettings");
        config["Secrets:FallbackFile"] = "nonexistent-operator-file.secrets.json";
        await Assert.ThrowsAsync<InvalidOperationException>(() => SecretBootstrap.LoadAsync(config, true));
        config["Secrets:FallbackFile"] = null;
        config["ConnectionStrings:DefaultConnection"] = "invalid-connection-sentinel";
        await Assert.ThrowsAsync<InvalidOperationException>(() => SecretBootstrap.LoadAsync(config, true));
        Assert.Null(config["Secrets:ResolvedSource"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Explicit_file_uses_absolute_base_and_ignores_working_directory(bool api)
    {
        var root = Path.Combine(Path.GetTempPath(), "vm-acme-secret-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "appsettings.secrets.json"), System.Text.Json.JsonSerializer.Serialize(new {
                ConnectionStrings = new { DefaultConnection = Connection }, Authentication = new { Jwt = new { SigningKey = Signing } }
            }));
            var config = Settings("AppSettings");
            config["Secrets:FallbackFile"] = "appsettings.secrets.json";
            config["Secrets:BaseDirectory"] = root;
            config["ConnectionStrings:DefaultConnection"] = "";
            await SecretBootstrap.LoadAsync(config, api);
            Assert.Equal(Connection, config.GetConnectionString("DefaultConnection"));
        }
        finally { Directory.Delete(root, true); }
    }
}
