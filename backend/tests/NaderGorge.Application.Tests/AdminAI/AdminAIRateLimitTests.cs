using NaderGorge.API.Configuration;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAIRateLimitTests
{
    [Theory]
    [InlineData("admin-ai-turn", 10, true)]
    [InlineData("admin-ai-confirmation", 20, true)]
    [InlineData("admin-ai-secure-input", 5, true)]
    [InlineData("admin-ai-internal", 120, false)]
    public void Policies_HaveClosedPartitionAndLimit(string name, int limit, bool byUser)
    {
        var policy = RateLimitingConfig.AdminAIPolicies.Single(x => x.Name == name);
        Assert.Equal(limit, policy.PermitLimit);
        Assert.Equal(byUser, policy.LimitByUser);
        Assert.Equal(TimeSpan.FromMinutes(1), policy.Window);
    }

    [Fact]
    public void ActiveTurnLimit_IsBoundedAndFailClosed() =>
        Assert.InRange(RateLimitingConfig.AdminAIActiveTurnLimit, 1, 3);
}
