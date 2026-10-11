using Microsoft.EntityFrameworkCore;
using VmAcme.Todos;
using VmAcme.Users;
using VmAcme.WorkItems;

namespace VmAcme.Persistence;

public sealed class VmAcmeDbContext(DbContextOptions<VmAcmeDbContext> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VmAcmeDbContext).Assembly);
    }
}