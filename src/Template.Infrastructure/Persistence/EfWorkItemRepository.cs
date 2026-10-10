using Microsoft.EntityFrameworkCore;
using Template.Application.WorkItems;
using Template.Domain.WorkItems;

namespace Template.Infrastructure.Persistence;

public sealed class EfWorkItemRepository(TemplateDbContext dbContext) : IWorkItemRepository
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
            .OrderBy(workItem => workItem.Title.Value)
            .ToArrayAsync(cancellationToken);

    public async Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken)
    {
        dbContext.WorkItems.Update(workItem);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
