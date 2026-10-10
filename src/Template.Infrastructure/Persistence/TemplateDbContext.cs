using Microsoft.EntityFrameworkCore;
using Template.Domain.WorkItems;

namespace Template.Infrastructure.Persistence;

public sealed class TemplateDbContext(DbContextOptions<TemplateDbContext> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TemplateDbContext).Assembly);
    }
}
