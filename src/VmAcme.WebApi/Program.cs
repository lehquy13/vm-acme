using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MediatR;
using VmAcme;
using VmAcme.Behaviors;
using VmAcme.Endpoints;
using VmAcme.WorkItems;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var jwtSection = builder.Configuration.GetSection("Authentication:Jwt");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];
var jwtSigningKey = jwtSection["SigningKey"];
if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience) ||
    string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException(
        "Authentication:Jwt requires Issuer, Audience, and a SigningKey of at least 32 UTF-8 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
var started = DateTimeOffset.UtcNow;
long requests = 0;
app.Use(async (context, next) =>
{
    Interlocked.Increment(ref requests);
    try
    {
        await next(context);
    }
    finally
    {
        app.Logger.LogInformation("HTTP {Method} {Path} returned {StatusCode}",
            context.Request.Method, context.Request.Path, context.Response.StatusCode);
    }
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapGet("/ready", async (ISender sender, CancellationToken token) =>
{
    try
    {
        await sender.Send(new ListWorkItemsQuery(), token);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        app.Logger.LogWarning(exception, "Readiness database check failed");
        return Results.StatusCode(503);
    }
}).AllowAnonymous();
app.MapGet("/version", () => Results.Ok(new
{
    version = typeof(VmAcme.Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion
})).AllowAnonymous();
app.MapGet("/metrics", (RequestMetrics metrics) => Results.Text(
    "# HELP vm_acme_http_requests_total Requests handled by this API process.\n" +
    "# TYPE vm_acme_http_requests_total counter\n" +
    $"vm_acme_http_requests_total {Interlocked.Read(ref requests)}\n" +
    "# HELP vm_acme_uptime_seconds Process uptime.\n" +
    "# TYPE vm_acme_uptime_seconds gauge\n" +
    $"vm_acme_uptime_seconds {(DateTimeOffset.UtcNow - started).TotalSeconds.ToString(CultureInfo.InvariantCulture)}\n" +
    metrics.ToPrometheusText(),
    "text/plain; version=0.0.4")).AllowAnonymous();
app.MapWorkItemEndpoints();

await app.RunAsync();

namespace VmAcme
{
    public partial class Program
    {
    }
}
