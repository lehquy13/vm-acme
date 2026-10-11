using System.Globalization;
using System.Reflection;
using VmAcme;
using VmAcme.Endpoints;
using VmAcme.WorkItems;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();
var started = DateTimeOffset.UtcNow;
long requests = 0;
app.Use(async (context, next) =>
{
    Interlocked.Increment(ref requests);
    await next(context);
    app.Logger.LogInformation("HTTP {Method} {Path} returned {StatusCode}",
        context.Request.Method, context.Request.Path, context.Response.StatusCode);
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/ready", async (ListWorkItems query, CancellationToken token) =>
{
    try
    {
        await query.HandleAsync(token);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        app.Logger.LogWarning(exception, "Readiness database check failed");
        return Results.StatusCode(503);
    }
});
app.MapGet("/version", () => Results.Ok(new
{
    version = typeof(VmAcme.Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion
}));
app.MapGet("/metrics", () => Results.Text(
    "# HELP vm_acme_http_requests_total Requests handled by this API process.\n" +
    "# TYPE vm_acme_http_requests_total counter\n" +
    $"vm_acme_http_requests_total {Interlocked.Read(ref requests)}\n" +
    "# HELP vm_acme_uptime_seconds Process uptime.\n" +
    "# TYPE vm_acme_uptime_seconds gauge\n" +
    $"vm_acme_uptime_seconds {(DateTimeOffset.UtcNow - started).TotalSeconds.ToString(CultureInfo.InvariantCulture)}\n",
    "text/plain; version=0.0.4"));
app.MapWorkItemEndpoints();

await app.RunAsync();

namespace VmAcme
{
    public partial class Program
    {
    }
}
