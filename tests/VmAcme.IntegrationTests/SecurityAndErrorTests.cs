using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using VmAcme.Authentication;
using VmAcme.Common;
using VmAcme.Todos;

namespace VmAcme;

public sealed class SecurityAndErrorTests
{
    [Theory]
    [InlineData("bad-signature")]
    [InlineData("bad-issuer")]
    [InlineData("bad-audience")]
    [InlineData("expired")]
    [InlineData("unknown-user")]
    [InlineData("invalid-subject")]
    [InlineData("valid")]
    public async Task Invalid_jwt_is_rejected(string scenario)
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        var userId = await TodoEndpointTests.SignIn(client);
        var options = factory.Services.GetRequiredService<JwtOptions>();
        var subject = userId.ToString();
        if (scenario == "invalid-subject")
            subject = "not-a-guid";
        else if (scenario == "unknown-user")
            subject = Guid.NewGuid().ToString();
        var key = scenario == "bad-signature" ? "another-signing-key-at-least-32-bytes-long" : options.SigningKey;
        var token = new JwtSecurityToken(
            scenario == "bad-issuer" ? "invalid" : options.Issuer,
            scenario == "bad-audience" ? "invalid" : options.Audience,
            [new Claim("sub", subject)], DateTime.UtcNow.AddHours(-2),
            scenario == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(scenario == "valid" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/todos")).StatusCode);
    }

    [Fact]
    public async Task Missing_permission_denies_authenticated_profile_request()
    {
        using var original = new VmAcmeWebApplicationFactory();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPermissionProvider>();
            services.AddScoped<IPermissionProvider, NoPermissions>();
        }));
        using var client = factory.CreateClient();
        var id = await TodoEndpointTests.SignIn(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/users/{id}")).StatusCode);
    }

    [Fact]
    public async Task Unknown_permission_fails_closed_for_authenticated_and_anonymous_principals()
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        var id = await TodoEndpointTests.SignIn(client);
        using var scope = factory.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"));
        Assert.False((await auth.AuthorizeAsync(user, null, "Permission:unknown:grant")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null,
            Permissions.UsersReadPolicy)).Succeeded);
    }

    [Fact]
    public async Task Unexpected_repository_error_returns_sanitized_problem_details()
    {
        var logs = new ConcurrentQueue<string>();
        using var original = new VmAcmeWebApplicationFactory();
        using var factory = original.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITodoRepository>();
            services.AddSingleton<ITodoRepository, BrokenRepository>();
            services.AddSingleton<ILoggerProvider>(new RecordingLoggerProvider(logs));
        }));
        using var client = factory.CreateClient();
        await TodoEndpointTests.SignIn(client);
        var response = await client.GetAsync("/api/todos");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password=secret", text);
        Assert.DoesNotContain("stack", text, StringComparison.OrdinalIgnoreCase);
        var body = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("An unexpected error occurred.", body.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        Assert.Contains(logs, line => line == "HTTP GET /api/todos returned 500");
    }

    [Theory]
    [InlineData("{invalid-json")]
    [InlineData("{\"description\":\"Todo\",\"priority\":\"not-an-enum\"}")]
    public async Task Malformed_payload_returns_problem_details(string json)
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        await TodoEndpointTests.SignIn(client);
        var response = await client.PostAsync("/api/todos", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/todos"))!);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Invalid_id_returns_problem_details(string id)
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        await TodoEndpointTests.SignIn(client);
        var response = await client.GetAsync($"/api/todos/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Past_due_date_and_oversized_labels_are_rejected()
    {
        using var factory = new VmAcmeWebApplicationFactory();
        using var client = factory.CreateClient();
        await TodoEndpointTests.SignIn(client);
        var past = await client.PostAsJsonAsync("/api/todos",
            new { description = "Past", dueDate = DateTimeOffset.UtcNow.AddDays(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        var labels = await client.PostAsJsonAsync("/api/todos",
            new { description = "Labels", labels = new[] { new string('x', 65) } });
        Assert.Equal(HttpStatusCode.BadRequest, labels.StatusCode);
    }

    private sealed class RecordingLoggerProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception));
    }

    private sealed class NoPermissions : IPermissionProvider
    {
        public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }

    private sealed class BrokenRepository : ITodoRepository
    {
        public Task AddAsync(TodoItem todo, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("password=secret");

        public Task<TodoItem?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("password=secret");

        public Task<IReadOnlyList<TodoItem>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("password=secret");

        public Task<Result<Unit>> UpdateAsync(TodoItem todo, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("password=secret");

        public Task<Result<Unit>> DeleteAsync(TodoItem todo, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("password=secret");
    }
}