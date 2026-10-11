using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VmAcme.Authentication;
using VmAcme.Persistence;
using VmAcme.Todos;

namespace VmAcme;

public sealed class HostConfigurationTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void InMemory_is_rejected_outside_local_environments(string environment)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Persistence:Provider"] = "InMemory" }).Build();
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuration, environment));
    }

    [Fact]
    public void Missing_provider_fails_startup_registration()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddInfrastructure(new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void Test_configuration_is_applied_before_identity_and_persistence_registration()
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal("vm-acme-tests", factory.Services.GetRequiredService<JwtOptions>().Issuer);
        Assert.IsType<InMemoryTodoRepository>(factory.Services.GetRequiredService<ITodoRepository>());
    }
}