using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Template.Application.WorkItems;
using Template.Infrastructure.Persistence;

namespace Template.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Persistence:Provider"];
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<InMemoryWorkItemRepository>();
            services.AddSingleton<IWorkItemRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<InMemoryWorkItemRepository>());
            return services;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"A connection string named 'DefaultConnection' is required for persistence provider '{provider}'.");
        }

        var normalizedProvider = provider.Trim();
        if (string.Equals(normalizedProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<TemplateDbContext>(options =>
                options.UseNpgsql(connectionString, db => db.MigrationsAssembly("Template.Migrations.PostgreSql")));
        }
        else if (string.Equals(normalizedProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<TemplateDbContext>(options =>
                options.UseSqlServer(connectionString, db => db.MigrationsAssembly("Template.Migrations.SqlServer")));
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown persistence provider '{provider}'. Use InMemory, PostgreSql, or SqlServer.");
        }

        services.AddScoped<IWorkItemRepository, EfWorkItemRepository>();
        return services;
    }
}
