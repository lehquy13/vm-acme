using Template.Application;
using Template.Infrastructure;
using Template.WebApi.Endpoints;

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
