using System.Net;

namespace VmAcme;

public sealed class OperationalEndpointTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    [InlineData("/version")]
    [InlineData("/metrics")]
    public async Task Operational_endpoint_is_anonymous(string route)
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);
    }
}