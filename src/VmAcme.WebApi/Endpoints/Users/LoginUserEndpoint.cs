using FastEndpoints;
using MediatR;
using VmAcme.Infrastructure;
using VmAcme.Users;

namespace VmAcme.Endpoints.Users;

public sealed class LoginUserRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public sealed class LoginUserEndpoint(ISender sender) : Endpoint<LoginUserRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/api/users/login");
        AllowAnonymous();
        Tags("Users");
    }

    public override async Task HandleAsync(LoginUserRequest request, CancellationToken ct) =>
        await Send.ResultAsync(
            (await sender.Send(new LoginUserCommand(request.Email, request.Password), ct)).ToHttp(
                HttpContext));
}