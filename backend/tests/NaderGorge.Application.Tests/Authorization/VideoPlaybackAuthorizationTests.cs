using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.API.Authorization;

namespace NaderGorge.Application.Tests.Authorization;

public sealed class VideoPlaybackAuthorizationTests
{
    [Theory]
    [InlineData("Admin", null, true, true)]
    [InlineData("Teacher", null, true, true)]
    [InlineData("Student", null, true, false)]
    [InlineData("Content editor", "content.manage", true, true)]
    [InlineData("Staff", "users.manage", false, false)]
    [InlineData(null, "content.manage", false, true)]
    public async Task PlaybackPolicyAllowsOnlyAuthenticatedStudentsOrAuthorizedContentPreview(
        string? role, string? permission, bool allowed, bool preview)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddVideoPlaybackPolicy());
        using var provider = services.BuildServiceProvider();
        var claims = new List<Claim>();
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (permission is not null) claims.Add(new Claim("permission", permission));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, role is null ? null : "test"));
        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, null, VideoPlaybackAuthorization.Policy);
        Assert.Equal(allowed, result.Succeeded);
        Assert.Equal(preview, VideoPlaybackAuthorization.CanPreview(principal));
    }
}
