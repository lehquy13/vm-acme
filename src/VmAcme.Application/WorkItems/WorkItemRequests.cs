using FluentValidation;
using MediatR;

namespace VmAcme.WorkItems;

public sealed record CreateWorkItemCommand(string? Title) : IRequest<WorkItemDto>;
public sealed record ListWorkItemsQuery : IRequest<IReadOnlyList<WorkItemDto>>;
public sealed record CompleteWorkItemCommand(Guid Id) : IRequest<WorkItemDto?>;

public sealed class CreateWorkItemCommandValidator : AbstractValidator<CreateWorkItemCommand>
{
    public CreateWorkItemCommandValidator()
    {
        RuleFor(command => command.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title is required.")
            .MaximumLength(WorkItemTitle.MaxLength);
    }
}

public sealed class CompleteWorkItemCommandValidator : AbstractValidator<CompleteWorkItemCommand>
{
    public CompleteWorkItemCommandValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}

public sealed class CreateWorkItemCommandHandler(CreateWorkItem useCase)
    : IRequestHandler<CreateWorkItemCommand, WorkItemDto>
{
    public Task<WorkItemDto> Handle(CreateWorkItemCommand request, CancellationToken cancellationToken) =>
        useCase.HandleAsync(request.Title!, cancellationToken);
}

public sealed class ListWorkItemsQueryHandler(ListWorkItems useCase)
    : IRequestHandler<ListWorkItemsQuery, IReadOnlyList<WorkItemDto>>
{
    public Task<IReadOnlyList<WorkItemDto>> Handle(ListWorkItemsQuery request, CancellationToken cancellationToken) =>
        useCase.HandleAsync(cancellationToken);
}

public sealed class CompleteWorkItemCommandHandler(CompleteWorkItem useCase)
    : IRequestHandler<CompleteWorkItemCommand, WorkItemDto?>
{
    public Task<WorkItemDto?> Handle(CompleteWorkItemCommand request, CancellationToken cancellationToken) =>
        useCase.HandleAsync(request.Id, cancellationToken);
}
