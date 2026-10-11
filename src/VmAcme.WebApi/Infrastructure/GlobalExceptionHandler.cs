using Microsoft.AspNetCore.Diagnostics;

namespace VmAcme.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested) return false;
        logger.LogError(exception, "Unhandled error for {Method} {Path}; trace {TraceId}", httpContext.Request.Method,
            httpContext.Request.Path, httpContext.TraceIdentifier);
        var response = exception is BadHttpRequestException
            ? Results.Problem(statusCode: 400, title: "The request is invalid.",
                extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier })
            : Results.Problem(statusCode: 500, title: "An unexpected error occurred.",
                extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier });
        await response.ExecuteAsync(httpContext);
        return true;
    }
}