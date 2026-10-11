using FastEndpoints;
using MediatR;
using VmAcme.Infrastructure;
using VmAcme.Users;

namespace VmAcme.Endpoints.Users;

public sealed class RegisterUserRequest
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Password { get; set; }
}

public sealed class RegisterUserEndpoint(ISender sender) : Endpoint<RegisterUserRequest, UserResponse>
{
    public override void Configure()
    {
        Post("/api/users/register");
        AllowAnonymous();
        Tags("Users");
    }

    public override async Task HandleAsync(RegisterUserRequest request, CancellationToken ct)
    {
        var result =
            await sender.Send(
                new RegisterUserCommand(request.Email, request.FirstName, request.LastName, request.Password),
                ct);
        await Send.ResultAsync(result.ToHttp(HttpContext, user => Results.Created($"/api/users/{user.Id}", user)));
    }
}