using Microsoft.Extensions.DependencyInjection;
using VmAcme.Authentication;
using VmAcme.Persistence;
using VmAcme.Todos;

namespace VmAcme;

public sealed class HostConfigurationTests
{
    [Fact]
    public void Test_configuration_is_applied_before_identity_and_persistence_registration()
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal("vm-acme-tests", factory.Services.GetRequiredService<JwtOptions>().Issuer);
        Assert.IsType<InMemoryTodoRepository>(factory.Services.GetRequiredService<ITodoRepository>());
    }
}