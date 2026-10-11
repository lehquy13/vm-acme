using Microsoft.EntityFrameworkCore;
using VmAcme.WorkItems;

namespace VmAcme.Persistence;

public sealed class EfWorkItemRepository(VmAcmeDbContext dbContext) : IWorkItemRepository
{
    public async Task AddAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        await dbContext.WorkItems.AddAsync(workItem, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<WorkItem?> GetAsync(WorkItemId id, CancellationToken cancellationToken) =>
        dbContext.WorkItems.SingleOrDefaultAsync(workItem => workItem.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WorkItem>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.WorkItems
            .OrderBy(workItem => workItem.Title)
            .ToArrayAsync(cancellationToken);

    public async Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        dbContext.WorkItems.Update(workItem);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException("Work item changed while it was being completed.", exception);
        }
    }
}