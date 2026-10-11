using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using VmAcme.Common;

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
            var response = await next(cancellationToken);
            logger.LogInformation("MediatR request {RequestName} completed with {Outcome} in {ElapsedMilliseconds} ms",
                requestName, response is IResultOutcome { IsFailure: true } ? "Failure" : "Success",
                stopwatch.Elapsed.TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "MediatR request {RequestName} failed after {ElapsedMilliseconds} ms",
                requestName, stopwatch.Elapsed.TotalMilliseconds);
            if (exception is OperationCanceledException canceled)
                throw new OperationCanceledException($"MediatR request {requestName} was canceled.", canceled,
                    canceled.CancellationToken);

            throw new InvalidOperationException($"MediatR request {requestName} failed.", exception);
        }
    }
}