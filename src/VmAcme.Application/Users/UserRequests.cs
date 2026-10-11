using FluentValidation;
using MediatR;
using VmAcme.Common;

namespace VmAcme.Users;

public sealed record UserResponse(Guid Id, string Email, string FirstName, string LastName)
{
    public static UserResponse From(User user) => new(user.Id, user.Email, user.FirstName, user.LastName);
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record RegisterUserCommand(string? Email, string? FirstName, string? LastName, string? Password)
    : IRequest<Result<UserResponse>>;

public sealed record LoginUserCommand(string? Email, string? Password) : IRequest<Result<LoginResponse>>;

public sealed record GetUserQuery(Guid Id) : IRequest<Result<UserResponse>>;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public sealed class LoginUserValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class GetUserValidator : AbstractValidator<GetUserQuery>
{
    public GetUserValidator() => RuleFor(x => x.Id).NotEmpty();
}