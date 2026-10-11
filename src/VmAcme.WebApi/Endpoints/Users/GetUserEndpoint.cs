using FastEndpoints;
using MediatR;
using VmAcme.Infrastructure;
using VmAcme.Users;

namespace VmAcme.Endpoints.Users;

public sealed class UserIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetUserEndpoint(ISender sender) : Endpoint<UserIdRequest, UserResponse>
{
    public override void Configure()
    {
        Get("/api/users/{id}");
        Policies(Authentication.Permissions.UsersReadPolicy);
        Tags("Users");
    }

    public override async Task HandleAsync(UserIdRequest request, CancellationToken ct) =>
        await Send.ResultAsync(
            (await sender.Send(new GetUserQuery(request.Id), ct)).ToHttp(HttpContext));
}