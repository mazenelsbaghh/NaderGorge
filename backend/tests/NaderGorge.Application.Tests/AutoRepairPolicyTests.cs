using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.AutoRepair;

namespace NaderGorge.Application.Tests;

public sealed class AutoRepairPolicyTests
{
    [Theory]
    [InlineData("diagnosing", "completed", false)]
    [InlineData("testing", "deploying", false)]
    [InlineData("ready", "deploying", true)]
    [InlineData("deploying", "monitoring", true)]
    [InlineData("monitoring", "completed", true)]
    [InlineData("failed", "deploying", false)]
    [InlineData("awaiting_approval", "deploying", false)]
    public void Completion_requires_verification_and_rollout(string before, string after, bool allowed) =>
        Assert.Equal(allowed, RepairPolicy.CanTransition(before, after));

    [Fact]
    public void Repeated_error_with_different_record_ids_groups_without_leaking_credentials()
    {
        var first = RepairPolicy.Redact("Failure record 123 token=secret-value\nemail=user@example.org phone=01012345678");
        var second = RepairPolicy.Redact("Failure record 456 token=other-value\nemail=user@example.org phone=01012345678");
        Assert.DoesNotContain("secret-value", first);
        Assert.DoesNotContain("user@example.org", first);
        Assert.DoesNotContain("01012345678", first);
        Assert.Equal(RepairPolicy.Fingerprint("backend", "Worker", first), RepairPolicy.Fingerprint("backend", "Worker", second));
        Assert.NotEqual(RepairPolicy.Fingerprint("backend", "Http", "HTTP 401"), RepairPolicy.Fingerprint("backend", "Http", "HTTP 500"));
        Assert.NotEqual(RepairPolicy.Fingerprint("backend", "Worker", first), RepairPolicy.Fingerprint("backend", "Payments", first));
    }

    [Theory]
    [InlineData("Unhandled exception. CorrelationId: e7ce5f9bb4a2497da9584303ccb815c8", "Unhandled exception. CorrelationId: a7ce5f9bb4a2497da9584303ccb815c9")]
    [InlineData("[14/Sep/2026:16:30:01 +0000] GET /health 500", "[14/Sep/2026:16:31:02 +0000] GET /health 500")]
    public void Same_production_error_groups_across_request_ids_and_log_timestamps(string first, string second)
    {
        Assert.Equal(RepairPolicy.Fingerprint("backend", "Http", first), RepairPolicy.Fingerprint("backend", "Http", second));
    }

    [Theory]
    [InlineData("", "", false)]
    [InlineData("short", "short", false)]
    [InlineData("0123456789abcdef0123456789abcdef", "wrong", false)]
    [InlineData("0123456789abcdef0123456789abcdef", "0123456789abcdef0123456789abcdef", true)]
    public async Task Machine_endpoint_requires_its_own_configured_credential(string configured, string supplied, bool accepted)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AutoRepair:RunnerToken"] = configured }).Build();
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Repair-Token"] = supplied;
        var context = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(context, [], new Dictionary<string, object?>(), new object());
        var reachedAction = false;
        await new RepairRunnerAuth(config).OnActionExecutionAsync(executing, () =>
        {
            reachedAction = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });
        Assert.Equal(accepted, reachedAction);
        if (!accepted) Assert.IsType<UnauthorizedResult>(executing.Result);
    }
}
