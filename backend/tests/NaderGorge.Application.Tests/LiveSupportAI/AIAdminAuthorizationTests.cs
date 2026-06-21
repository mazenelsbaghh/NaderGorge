using Microsoft.AspNetCore.Authorization;
using NaderGorge.API.Controllers;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class AIAdminAuthorizationTests
{
    [Fact]
    public void Configuration_controller_requires_only_the_builtin_admin_role()
    {
        var attribute = Assert.Single(typeof(LiveSupportAIAdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", attribute.Roles);
        Assert.Null(attribute.Policy);
    }
}
