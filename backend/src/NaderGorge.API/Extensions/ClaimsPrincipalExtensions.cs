using System.Security.Claims;

namespace NaderGorge.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid RequireUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId) || userId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authenticated user id claim is missing or invalid.");
        }

        return userId;
    }
}
