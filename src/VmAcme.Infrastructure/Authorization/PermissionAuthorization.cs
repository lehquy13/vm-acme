using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VmAcme.Authentication;
using VmAcme.Users;

namespace VmAcme.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    private const string Prefix = "Permission:";
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policies = new(StringComparer.Ordinal);

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal)) return base.GetPolicyAsync(policyName);
        var policy = _policies.GetOrAdd(policyName, name => new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(name[Prefix.Length..])).Build());
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

public sealed class PermissionAuthorizationHandler(IPermissionProvider permissions, IHttpContextAccessor accessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty) return;
        var grants =
            await permissions.GetPermissionsAsync(userId,
                accessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        if (grants.Contains(requirement.Permission)) context.Succeed(requirement);
    }
}

// Example grants are resolved server-side. Replace with role/permission storage for your project.
public sealed class PermissionProvider(IUserRepository users) : IPermissionProvider
{
    public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.GetByIdAsync(userId, cancellationToken) is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>([Permissions.UsersRead], StringComparer.Ordinal);
}