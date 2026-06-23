using System.Security.Claims;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.API.Services;

internal sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public string UserId
    {
        get
        {
            var user = accessor.HttpContext?.User
                ?? throw new InvalidOperationException("No authenticated user in context.");

            // Try the unmapped JWT "sub" claim first; fall back to the mapped ClaimTypes.NameIdentifier.
            return user.FindFirst("sub")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new InvalidOperationException("JWT 'sub' claim is missing.");
        }
    }
}
