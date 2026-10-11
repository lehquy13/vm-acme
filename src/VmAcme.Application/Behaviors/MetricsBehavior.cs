using System.Diagnostics;
using MediatR;
using VmAcme.Common;

namespace VmAcme.Behaviors;

public sealed class MetricsBehavior<TRequest, TResponse>(RequestMetrics metrics)
    : IPipelineBehavior<TRequest, TResponse>
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
            var response = await next(cancellationToken);
            failed = response is IResultOutcome { IsFailure: true };
            return response;
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