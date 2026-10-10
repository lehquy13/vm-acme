using Acme.Application;
using Acme.Infrastructure;
using Acme.WebApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapWorkItemEndpoints();

app.Run();

public partial class Program
{
}
