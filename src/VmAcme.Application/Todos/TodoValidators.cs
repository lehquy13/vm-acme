using FluentValidation;

namespace VmAcme.Todos;

public sealed class CreateTodoValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoValidator(TimeProvider time)
    {
        RuleFor(x => x.Description).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(255);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.DueDate).Must(x => x is null || x.Value.UtcDateTime.Date >= time.GetUtcNow().UtcDateTime.Date)
            .WithMessage("Due date cannot be before today (UTC).");
        RuleFor(x => x.Labels).Must(x => x is null || x.Length <= 20).WithMessage("At most 20 labels are allowed.");
        RuleForEach(x => x.Labels).NotEmpty().MaximumLength(64);
    }
}

public sealed class GetTodoValidator : AbstractValidator<GetTodoQuery>
{
    public GetTodoValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class CompleteTodoValidator : AbstractValidator<CompleteTodoCommand>
{
    public CompleteTodoValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class DeleteTodoValidator : AbstractValidator<DeleteTodoCommand>
{
    public DeleteTodoValidator() => RuleFor(x => x.Id).NotEmpty();
}