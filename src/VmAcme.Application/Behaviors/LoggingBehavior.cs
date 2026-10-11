using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace VmAcme.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            logger.LogInformation("MediatR request {RequestName} completed in {ElapsedMilliseconds} ms",
                requestName, stopwatch.Elapsed.TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "MediatR request {RequestName} failed after {ElapsedMilliseconds} ms",
                requestName, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
