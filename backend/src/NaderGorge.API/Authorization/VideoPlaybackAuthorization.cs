using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace NaderGorge.API.Authorization;

public static class VideoPlaybackAuthorization
{
    public const string Policy = "VideoPlayback";

    public static bool CanPreview(ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("Teacher")
        || user.HasClaim(claim => claim.Type == "permission"
            && claim.Value.Equals("content.manage", StringComparison.OrdinalIgnoreCase));

    public static void AddVideoPlaybackPolicy(this AuthorizationOptions options) =>
        options.AddPolicy(Policy, policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.IsInRole("Student") || CanPreview(context.User)));
}
