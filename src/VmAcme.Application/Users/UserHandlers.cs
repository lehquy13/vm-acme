using MediatR;
using VmAcme.Authentication;
using VmAcme.Common;

namespace VmAcme.Users;

public sealed class RegisterUserHandler(IUserRepository users, IPasswordHasher hasher)
    : IRequestHandler<RegisterUserCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var user = User.Create(request.Email!, request.FirstName!, request.LastName!, hasher.Hash(request.Password!));
        var saved = await users.AddAsync(user, cancellationToken);
        return saved.IsFailure
            ? Result<UserResponse>.Failure(saved.Error!)
            : Result<UserResponse>.Success(UserResponse.From(user));
    }
}

public sealed class LoginUserHandler(IUserRepository users, IPasswordHasher hasher, ITokenProvider tokens)
    : IRequestHandler<LoginUserCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(User.NormalizeEmail(request.Email!), cancellationToken);
        if (user is null || !hasher.Verify(request.Password!, user.PasswordHash))
            return Result<LoginResponse>.Failure(new Error("Users.InvalidCredentials",
                "Email or password is incorrect.", ErrorType.Unauthorized));
        var token = tokens.Create(user);
        return Result<LoginResponse>.Success(new LoginResponse(token.AccessTokenValue, token.ExpiresAt));
    }
}

public sealed class GetUserHandler(IUserRepository users, IUserContext context)
    : IRequestHandler<GetUserQuery, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        if (context.UserId is null)
            return Result<UserResponse>.Failure(new Error("Authentication.Required", "Authentication is required.",
                ErrorType.Unauthorized));
        var user = context.UserId == request.Id ? await users.GetByIdAsync(request.Id, cancellationToken) : null;
        return user is null
            ? Result<UserResponse>.Failure(new Error("Users.NotFound", "User was not found.", ErrorType.NotFound))
            : Result<UserResponse>.Success(UserResponse.From(user));
    }
}