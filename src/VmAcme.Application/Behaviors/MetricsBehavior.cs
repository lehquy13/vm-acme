using System.Diagnostics;
using MediatR;

namespace VmAcme.Behaviors;

public sealed class MetricsBehavior<TRequest, TResponse>(RequestMetrics metrics) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var failed = false;
        try
        {
            return await next();
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            metrics.Record(typeof(TRequest).Name, stopwatch.Elapsed.TotalMilliseconds, failed);
        }
    }
}
