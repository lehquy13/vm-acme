using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace VmAcme;

public sealed class VmAcmeWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:Jwt:Issuer", "vm-acme-tests");
        builder.UseSetting("Authentication:Jwt:Audience", "vm-acme-tests");
        builder.UseSetting("Authentication:Jwt:SigningKey", "tests-only-signing-key-at-least-32-bytes-long");
        builder.UseSetting("Persistence:Provider", "InMemory");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = null,
                ["ConnectionStrings:DefaultConnection"] = null,
                ["Authentication:Jwt:Issuer"] = "vm-acme-tests",
                ["Authentication:Jwt:Audience"] = "vm-acme-tests",
                ["Authentication:Jwt:SigningKey"] = "tests-only-signing-key-at-least-32-bytes-long"
            }));
    }
}