using Microsoft.AspNetCore.Http;

namespace VmAcme.Authentication;

public sealed class UserContext(IHttpContextAccessor accessor) : IUserContext
{
    public Guid? UserId => accessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal &&
                           Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) && id != Guid.Empty
        ? id
        : null;
}