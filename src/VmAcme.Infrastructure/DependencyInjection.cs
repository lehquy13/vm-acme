using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VmAcme.Persistence;
using VmAcme.Todos;
using VmAcme.Users;
using VmAcme.WorkItems;

namespace VmAcme;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName = "Production")
    {
        var provider = configuration["Persistence:Provider"];
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(provider))
            throw new InvalidOperationException("Persistence:Provider is required.");
        provider = provider.Trim();
        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            if (environmentName is not ("Development" or "Testing"))
                throw new InvalidOperationException("InMemory persistence is allowed only in Development or Testing.");
            services.AddSingleton<ITodoRepository, InMemoryTodoRepository>();
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
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

        services.AddSingleton<DatabaseCommandMetrics>();

        var normalizedProvider = provider.Trim();
        if (string.Equals(normalizedProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            services.AddPooledDbContextFactory<VmAcmeDbContext>((serviceProvider, options) =>
            {
                options.UseNpgsql(connectionString, db => db.MigrationsAssembly("VmAcme.Migrations.PostgreSql")
                    .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
                options.AddInterceptors(serviceProvider.GetRequiredService<DatabaseCommandMetrics>());
            });
        }
        else if (string.Equals(normalizedProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddPooledDbContextFactory<VmAcmeDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(connectionString, db => db.MigrationsAssembly("VmAcme.Migrations.SqlServer")
                    .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
                options.AddInterceptors(serviceProvider.GetRequiredService<DatabaseCommandMetrics>());
            });
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown persistence provider '{provider}'. Use InMemory, PostgreSql, or SqlServer.");
        }

        services.AddScoped<IWorkItemRepository, EfWorkItemRepository>();
        services.AddScoped<ITodoRepository, EfTodoRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        return services;
    }
}