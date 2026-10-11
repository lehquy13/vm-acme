using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.Metrics;
using System.Data.Common;

namespace VmAcme.Configuration;

public static class SecretBootstrap
{
    private static readonly Meter Meter = new("VmAcme.Secrets");
    private static readonly Counter<long> Fallbacks = Meter.CreateCounter<long>("secrets.fallback.activations");

    public static ConfigurationManager CreateConfiguration()
    {
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(AppContext.BaseDirectory);
        configuration.AddJsonFile("appsettings.json", optional: true);
        configuration.AddEnvironmentVariables();
        return configuration;
    }

    public static async Task LoadAsync(ConfigurationManager configuration, bool includeJwt,
        Func<Uri, string, CancellationToken, Task<string>>? readSecret = null,
        CancellationToken cancellationToken = default, Action<string>? warning = null)
    {
        var mode = configuration["Secrets:Mode"] ?? "AppSettings";
        if (mode is not ("AppSettings" or "KeyVaultRequired" or "KeyVaultWithFallback"))
            throw new InvalidOperationException("Invalid Secrets:Mode.");

        // Snapshot settings without publishing file or vault values until a complete set is validated.
        Dictionary<string, string?> LoadFallback()
        {
            var fallback = new ConfigurationBuilder().AddConfiguration(configuration).Build();
            var file = configuration["Secrets:FallbackFile"];
            if (!string.IsNullOrWhiteSpace(file))
            {
                var baseDirectory = configuration["Secrets:BaseDirectory"] ?? AppContext.BaseDirectory;
                if (!Path.IsPathFullyQualified(baseDirectory)) throw new InvalidOperationException("Secrets:BaseDirectory must be absolute.");
                var path = Path.GetFullPath(file, baseDirectory);
                try
                {
                    // File overrides tracked settings; environment overrides the file.
                    fallback = new ConfigurationBuilder().AddConfiguration(configuration)
                        .AddJsonFile(path, optional: false, reloadOnChange: false).AddEnvironmentVariables().Build();
                }
                catch (Exception) { throw new InvalidOperationException("The configured secrets file could not be loaded."); }
            }
            return Validate(fallback, includeJwt, configuration["Persistence:Provider"]);
        }
        if (mode == "AppSettings")
        {
            Publish(LoadFallback(), "AppSettings");
            return;
        }
        if (!Uri.TryCreate(configuration["Secrets:VaultUri"], UriKind.Absolute, out var vault) ||
            vault.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(vault.UserInfo) ||
            vault.AbsolutePath != "/" || !string.IsNullOrEmpty(vault.Query))
            throw new InvalidOperationException("Secrets:VaultUri must be an HTTPS vault root URI.");
        var connectionName = Required(configuration, "Secrets:ConnectionSecret");
        var signingName = includeJwt ? Required(configuration, "Secrets:JwtSecret") : null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            if (readSecret is null)
            {
                var client = new SecretClient(vault, new WorkloadIdentityCredential(), new SecretClientOptions
                {
                    Retry = { MaxRetries = 3, NetworkTimeout = TimeSpan.FromSeconds(5) }
                });
                readSecret = async (_, name, token) =>
                    (await client.GetSecretAsync(name, cancellationToken: token)).Value.Value;
            }
            var values = new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = await Read(connectionName) };
            if (signingName is not null) values["Authentication:Jwt:SigningKey"] = await Read(signingName);
            var candidate = new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection(values).Build();
            Publish(Validate(candidate, includeJwt, configuration["Persistence:Provider"]), "KeyVault");
        }
        catch (Exception)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Secret resolution canceled.", cancellationToken);
            if (mode != "KeyVaultWithFallback") throw new InvalidOperationException("Required Key Vault configuration could not be resolved.");
            var values = LoadFallback();
            Publish(values, "Fallback");
            (warning ?? Console.Error.WriteLine)($"SECRET_FALLBACK_ACTIVE=1 execution={(includeJwt ? "api" : "migration")}: explicitly enabled application settings fallback activated.");
            Fallbacks.Add(1, new KeyValuePair<string, object?>("execution", includeJwt ? "api" : "migration"));
        }

        void Publish(Dictionary<string, string?> values, string source)
        {
            values["Secrets:ResolvedSource"] = source;
            configuration.AddInMemoryCollection(values);
        }

        async Task<string> Read(string name)
        {
            var value = await readSecret(vault, name, deadline.Token).WaitAsync(deadline.Token);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("A required vault secret is empty.");
            return value;
        }
    }

    private static Dictionary<string, string?> Validate(IConfiguration configuration, bool includeJwt, string? provider)
    {
        var values = new Dictionary<string, string?>();
        if (!includeJwt || !string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var connection = configuration.GetConnectionString("DefaultConnection");
            try
            {
                if (string.IsNullOrWhiteSpace(connection)) throw new FormatException();
                _ = new DbConnectionStringBuilder { ConnectionString = connection };
                if (provider == "PostgreSql")
                {
                    var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connection);
                    if (string.IsNullOrWhiteSpace(parsed.Host) || string.IsNullOrWhiteSpace(parsed.Database)) throw new FormatException();
                }
                else if (provider == "SqlServer")
                {
                    var parsed = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
                    if (string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog)) throw new FormatException();
                }
            }
            catch (Exception) { throw new InvalidOperationException("Required database settings are invalid or incomplete."); }
            values["ConnectionStrings:DefaultConnection"] = connection;
        }
        if (includeJwt)
        {
            var key = configuration["Authentication:Jwt:SigningKey"];
            if (string.IsNullOrWhiteSpace(key) || System.Text.Encoding.UTF8.GetByteCount(key) < 32)
                throw new InvalidOperationException("Required signing settings are invalid or incomplete.");
            values["Authentication:Jwt:SigningKey"] = key;
        }
        return values;
    }

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"{key} is required in a Key Vault mode.");
}
