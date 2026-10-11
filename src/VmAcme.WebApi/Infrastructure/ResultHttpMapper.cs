using VmAcme.Common;

namespace VmAcme.Infrastructure;

public static class ResultHttpMapper
{
    public static IResult ToHttp<T>(this Result<T> result, HttpContext context, Func<T, IResult>? success = null) =>
        result.IsSuccess ? (success ?? (value => Results.Ok(value)))(result.Value) : Problem(result.Error!, context);

    private static IResult Problem(Error error, HttpContext context)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => 400, ErrorType.Unauthorized => 401, ErrorType.Forbidden => 403,
            ErrorType.NotFound => 404, ErrorType.Conflict => 409, _ => 500
        };
        var extensions = new Dictionary<string, object?>
            { ["code"] = error.Code, ["traceId"] = context.TraceIdentifier };
        if (error.ValidationErrors is not null) extensions["errors"] = error.ValidationErrors;
        return Results.Problem(statusCode: status, title: error.Description, extensions: extensions);
    }
}