using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace VmAcme;

public sealed class TodoEndpointTests
{
    internal static async Task<Guid> SignIn(HttpClient client, string email = "owner@example.com")
    {
        var registration = await client.PostAsJsonAsync("/api/users/register", new
        {
            email, firstName = "Todo", lastName = "Owner", password = "Strong-password-123!"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var user = await registration.Content.ReadFromJsonAsync<JsonElement>();
        var login = await client.PostAsJsonAsync("/api/users/login", new { email, password = "Strong-password-123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        return user.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Register_login_and_todo_lifecycle()
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        var ownerId = await SignIn(client);
        var response = await client.PostAsJsonAsync("/api/todos", new
        {
            description = "Write architecture tests", priority = 3,
            dueDate = DateTimeOffset.UtcNow.AddDays(1), labels = new[] { "engineering" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = todo.GetProperty("id").GetGuid();
        Assert.Equal(ownerId, todo.GetProperty("userId").GetGuid());
        Assert.Equal("Write architecture tests", todo.GetProperty("description").GetString());
        Assert.Equal(3, todo.GetProperty("priority").GetInt32());
        Assert.False(todo.GetProperty("isCompleted").GetBoolean());
        Assert.Equal($"/api/todos/{id}", response.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/todos/{id}")).StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/todos");
        Assert.Single(list!);
        var completed = await client.PostAsync($"/api/todos/{id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var body = await completed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("completedAt").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/todos/{id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/todos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/todos/{id}")).StatusCode);
    }

    [Fact]
    public async Task Other_user_cannot_read_complete_or_delete_todos()
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var owner = factory.CreateClient();
        await SignIn(owner);
        var created = await owner.PostAsJsonAsync("/api/todos", new { description = "Private todo" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var todo = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = todo.GetProperty("id").GetGuid();
        using var other = factory.CreateClient();
        await SignIn(other, "other@example.com");
        Assert.Empty((await other.GetFromJsonAsync<JsonElement[]>("/api/todos"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/todos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/todos/{id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/todos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/todos/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("/api/todos")]
    [InlineData("/api/users/11111111-1111-1111-1111-111111111111")]
    public async Task Anonymous_access_is_denied(string path)
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData(" ", 0)]
    [InlineData("Valid", 99)]
    public async Task Invalid_todo_returns_problem_details_without_persisting(string description, int priority)
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        await SignIn(client);
        var response = await client.PostAsJsonAsync("/api/todos", new { description, priority });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/todos"))!);
    }

    [Fact]
    public async Task Registration_duplicate_email_and_invalid_credentials_are_handled()
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        await SignIn(client);
        var duplicate = await client.PostAsJsonAsync("/api/users/register", new
        {
            email = "OWNER@example.com", firstName = "Other", lastName = "Name", password = "Strong-password-123!"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var login = await client.PostAsJsonAsync("/api/users/login",
            new { email = "owner@example.com", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.DoesNotContain("hash", await login.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task User_can_read_only_their_own_profile()
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var owner = factory.CreateClient();
        var id = await SignIn(owner);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/users/{id}")).StatusCode);
        using var other = factory.CreateClient();
        await SignIn(other, "other@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/users/{id}")).StatusCode);
    }
}