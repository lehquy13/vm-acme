using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace VmAcme.Persistence;

public sealed class DatabaseCommandMetrics : DbCommandInterceptor
{
    private static readonly Meter Meter = new("VmAcme.Database");

    private static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>("db.client.operation.duration", "s");

    private readonly ConditionalWeakTable<DbCommand, ExecutionStart> _starts = new();
    private readonly object _startsLock = new();

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Start(command, "nonquery");
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Start(command, "nonquery");
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Complete(command, failed: false);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Complete(command, failed: false);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Start(command, "reader");
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Start(command, "reader");
        return ValueTask.FromResult(result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Complete(command, failed: false);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Complete(command, failed: false);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Start(command, "scalar");
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Start(command, "scalar");
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Complete(command, failed: false);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Complete(command, failed: false);
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) =>
        Complete(command, failed: true, eventData.Exception.GetType().Name);

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Complete(command, failed: true, eventData.Exception.GetType().Name);
        return Task.CompletedTask;
    }

    public override void CommandCanceled(DbCommand command, CommandEndEventData eventData) =>
        Complete(command, failed: false);

    public override Task CommandCanceledAsync(
        DbCommand command,
        CommandEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Complete(command, failed: false);
        return Task.CompletedTask;
    }

    private void Start(DbCommand command, string operation)
    {
        var start = new ExecutionStart(Stopwatch.GetTimestamp(), GetDatabaseSystem(command), operation);
        lock (_startsLock)
        {
            _starts.Remove(command);
            _starts.Add(command, start);
        }
    }

    private void Complete(DbCommand command, bool failed, string? errorType = null)
    {
        ExecutionStart? start;
        lock (_startsLock)
        {
            if (!_starts.TryGetValue(command, out start))
                return;
            _starts.Remove(command);
        }

        var tags = new TagList
        {
            { "db.system.name", start.DatabaseSystem },
            { "db.operation.name", start.Operation }
        };
        if (failed)
            tags.Add("error.type", errorType ?? "unknown");

        OperationDuration.Record(Stopwatch.GetElapsedTime(start.Timestamp).TotalSeconds, tags);
    }

    private static string GetDatabaseSystem(DbCommand command)
    {
        var typeName = command.Connection?.GetType().FullName;
        if (typeName?.StartsWith("Npgsql.", StringComparison.Ordinal) == true)
            return "postgresql";
        if (typeName?.StartsWith("Microsoft.Data.SqlClient.", StringComparison.Ordinal) == true
            || typeName?.StartsWith("System.Data.SqlClient.", StringComparison.Ordinal) == true)
            return "microsoft.sql_server";
        return "other";
    }

    private sealed record ExecutionStart(long Timestamp, string DatabaseSystem, string Operation);
}