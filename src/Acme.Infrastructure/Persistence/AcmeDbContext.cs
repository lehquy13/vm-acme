using Microsoft.EntityFrameworkCore;
using Acme.Domain.WorkItems;

namespace Acme.Infrastructure.Persistence;

public sealed class AcmeDbContext(DbContextOptions<AcmeDbContext> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcmeDbContext).Assembly);
    }
}
