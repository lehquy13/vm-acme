using Microsoft.Extensions.DependencyInjection;
using Template.Application.WorkItems;

namespace Template.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateWorkItem>();
        services.AddScoped<ListWorkItems>();
        services.AddScoped<CompleteWorkItem>();
        return services;
    }
}
