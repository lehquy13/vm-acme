using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;

namespace VmAcme.Configuration;

public static class SecretBootstrap
{
    public static async Task LoadAsync(ConfigurationManager configuration, bool includeJwt,
        Func<Uri, string, CancellationToken, Task<string>>? readSecret = null,
        CancellationToken cancellationToken = default)
    {
        var mode = configuration["Secrets:Mode"] ?? "Environment";
        if (mode == "Environment") return;
        if (mode != "KeyVault") throw new InvalidOperationException("Secrets:Mode must be Environment or KeyVault.");
        if (!Uri.TryCreate(configuration["Secrets:VaultUri"], UriKind.Absolute, out var vault) ||
            vault.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(vault.UserInfo) ||
            vault.AbsolutePath != "/" || !string.IsNullOrEmpty(vault.Query))
            throw new InvalidOperationException("Secrets:VaultUri must be an HTTPS vault root URI.");
        var connectionName = Required(configuration, "Secrets:ConnectionSecret");
        var signingName = includeJwt ? Required(configuration, "Secrets:JwtSecret") : null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        if (readSecret is null)
        {
            var client = new SecretClient(vault, new WorkloadIdentityCredential(), new SecretClientOptions
            {
                Retry = { MaxRetries = 3, NetworkTimeout = TimeSpan.FromSeconds(5) }
            });
            readSecret = async (_, name, token) =>
                (await client.GetSecretAsync(name, cancellationToken: token)).Value.Value;
        }

        // Publish atomically after all required reads succeed. Never fall back on stale environment secrets.
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = await Read(connectionName)
        };
        if (signingName is not null) values["Authentication:Jwt:SigningKey"] = await Read(signingName);
        configuration.AddInMemoryCollection(values);

        async Task<string> Read(string name)
        {
            var value = await readSecret(vault, name, deadline.Token);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("A required vault secret is empty.");
            return value;
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"{key} is required in KeyVault mode.");
}