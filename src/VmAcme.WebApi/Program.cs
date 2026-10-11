using System.Globalization;
using System.Reflection;
using FastEndpoints;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using VmAcme;
using VmAcme.Authentication;
using VmAcme.Behaviors;
using VmAcme.Configuration;
using VmAcme.Infrastructure;
using VmAcme.Telemetry;
using VmAcme.Todos;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

var builder = WebApplication.CreateBuilder(args);
await SecretBootstrap.LoadAsync(builder.Configuration, includeJwt: true);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("vm-acme-api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation()
        .AddSqlClientInstrumentation().AddSource("Npgsql")
        .AddProcessor(new DatabaseTraceRedactor()).AddOtlpExporter())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation()
        .AddMeter("System.Runtime", "Npgsql", "Microsoft.Data.SqlClient", "VmAcme.Database", "VmAcme.Secrets").AddOtlpExporter());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName)
    .AddIdentityServices(builder.Configuration);
builder.Services.AddFastEndpoints();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(options =>
{
    options.Errors.ContentType = "application/problem+json";
    options.Errors.ResponseBuilder = (failures, context, status) => new ProblemDetails
    {
        Status = status, Title = "One or more validation errors occurred.",
        Extensions =
        {
            ["code"] = "Validation.Failed", ["traceId"] = context.TraceIdentifier,
            ["errors"] = failures.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).Distinct().ToArray())
        }
    };
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapGet("/ready", async (ITodoRepository todos, CancellationToken token) =>
{
    try
    {
        await todos.ListAsync(Guid.Empty, token);
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
    $"vm_acme_secret_fallback_active {(builder.Configuration["Secrets:ResolvedSource"] == "Fallback" ? 1 : 0)}\n" +
    "# HELP vm_acme_http_requests_total Requests handled by this API process.\n" +
    "# TYPE vm_acme_http_requests_total counter\n" +
    $"vm_acme_http_requests_total {Interlocked.Read(ref requests)}\n" +
    "# HELP vm_acme_uptime_seconds Process uptime.\n" +
    "# TYPE vm_acme_uptime_seconds gauge\n" +
    $"vm_acme_uptime_seconds {(DateTimeOffset.UtcNow - started).TotalSeconds.ToString(CultureInfo.InvariantCulture)}\n" +
    metrics.ToPrometheusText(),
    "text/plain; version=0.0.4")).AllowAnonymous();

await app.RunAsync();

namespace VmAcme
{
    public partial class Program
    {
    }
}
