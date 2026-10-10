using System.Net;
using System.Net.Http.Json;

namespace Template.IntegrationTests;

public sealed class WorkItemEndpointTests
{
    [Fact]
    public async Task Health_is_available_without_database_configuration()
    {
        using var factory = new TemplateWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_list_and_complete_work_item()
    {
        using var factory = new TemplateWebApplicationFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/work-items", new { title = "Write integration tests" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.NotNull(created);
        Assert.Equal("Open", created.Status);

        var list = await client.GetFromJsonAsync<WorkItemResponse[]>("/api/work-items");
        Assert.NotNull(list);
        Assert.Contains(list, item => item.Id == created.Id);

        var completeResponse = await client.PostAsync($"/api/work-items/{created.Id}/complete", content: null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completed = await completeResponse.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.Equal("Completed", completed?.Status);
    }

    [Fact]
    public async Task Invalid_title_returns_bad_request()
    {
        using var factory = new TemplateWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/work-items", new { title = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Completing_missing_work_item_returns_not_found()
    {
        using var factory = new TemplateWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/work-items/{Guid.NewGuid()}/complete", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Completing_work_item_twice_returns_bad_request()
    {
        using var factory = new TemplateWebApplicationFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/work-items", new { title = "One time" });
        var created = await createResponse.Content.ReadFromJsonAsync<WorkItemResponse>();
        Assert.NotNull(created);
        await client.PostAsync($"/api/work-items/{created.Id}/complete", content: null);

        var response = await client.PostAsync($"/api/work-items/{created.Id}/complete", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record WorkItemResponse(Guid Id, string Title, string Status);
}
