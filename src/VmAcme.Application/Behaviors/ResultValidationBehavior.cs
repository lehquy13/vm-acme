using FluentValidation;
using FluentValidation.Results;
using MediatR;
using VmAcme.Common;

namespace VmAcme.Behaviors;

public sealed class ResultValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IValidationResult<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var errors = new List<ValidationFailure>();
        foreach (var validator in validators)
            errors.AddRange((await validator.ValidateAsync(request, cancellationToken)).Errors);
        if (errors.Count == 0) return await next(cancellationToken);
        return TResponse.Failure(
            new Error("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation)
            {
                ValidationErrors = errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key,
                    x => x.Select(e => e.ErrorMessage).Distinct().ToArray())
            });
    }
}