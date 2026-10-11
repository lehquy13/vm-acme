using Microsoft.EntityFrameworkCore;
using VmAcme.WorkItems;

namespace VmAcme.Persistence;

public sealed class VmAcmeDbContext(DbContextOptions<VmAcmeDbContext> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VmAcmeDbContext).Assembly);
    }
}