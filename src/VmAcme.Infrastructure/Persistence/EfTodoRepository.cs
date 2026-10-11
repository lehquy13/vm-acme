using Microsoft.EntityFrameworkCore;
using VmAcme.Common;
using VmAcme.Todos;

namespace VmAcme.Persistence;

public sealed class EfTodoRepository(VmAcmeDbContext db) : ITodoRepository
{
    public async Task AddAsync(TodoItem todo, CancellationToken cancellationToken)
    {
        db.TodoItems.Add(todo);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<TodoItem?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        db.TodoItems.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<TodoItem>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.TodoItems.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);

    public async Task<Result<Unit>> UpdateAsync(TodoItem todo, CancellationToken cancellationToken)
    {
        db.TodoItems.Update(todo);
        return await SaveAsync(cancellationToken);
    }

    public async Task<Result<Unit>> DeleteAsync(TodoItem todo, CancellationToken cancellationToken)
    {
        db.TodoItems.Remove(todo);
        return await SaveAsync(cancellationToken);
    }

    private async Task<Result<Unit>> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result<Unit>.Success(new Unit());
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Unit>.Failure(new Error("Todos.ConcurrentChange",
                "Todo changed during this operation. Reload and retry.", ErrorType.Conflict));
        }
    }
}