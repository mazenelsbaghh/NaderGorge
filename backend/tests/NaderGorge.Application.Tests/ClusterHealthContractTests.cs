using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using StackExchange.Redis;

namespace NaderGorge.Application.Tests;

public sealed class ClusterHealthContractTests
{
    private static readonly IConfiguration IdentityConfiguration =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cluster:NodeId"] = "node-contract",
                ["Cluster:ReleaseId"] = "git-abcdef1"
            })
            .Build();

    [Fact]
    public void LiveContract_ContainsStableStatusNodeReleaseAndTimestamp()
    {
        var controller = new HealthController(null!, null!, IdentityConfiguration);

        var result = Assert.IsType<OkObjectResult>(controller.GetLive());
        var json = JsonSerializer.SerializeToElement(result.Value);

        Assert.Equal("healthy", json.GetProperty("status").GetString());
        Assert.Equal("node-contract", json.GetProperty("nodeId").GetString());
        Assert.Equal("git-abcdef1", json.GetProperty("releaseId").GetString());
        Assert.Equal(JsonValueKind.String, json.GetProperty("timestamp").ValueKind);
    }

    [Fact]
    public async Task ReadyContract_Returns503AndNamesEveryFailedDependency()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable;Username=none;Password=none;Timeout=1")
            .Options;
        await using var database = new AppDbContext(options);
        using var redis = await ConnectionMultiplexer.ConnectAsync(
            "127.0.0.1:1,abortConnect=false,connectTimeout=50,syncTimeout=50");
        var controller = new HealthController(database, redis, IdentityConfiguration);

        var result = Assert.IsType<ObjectResult>(await controller.GetReady());
        var json = JsonSerializer.SerializeToElement(result.Value);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("unhealthy", json.GetProperty("status").GetString());
        Assert.Equal("unhealthy", json.GetProperty("database").GetString());
        Assert.Equal("unhealthy", json.GetProperty("redis").GetString());
        Assert.Equal("node-contract", json.GetProperty("nodeId").GetString());
        Assert.Equal("git-abcdef1", json.GetProperty("releaseId").GetString());
    }

    [Fact]
    public async Task AdminAIReady_WhenDisabled_IsHealthyWithoutDisclosingConfiguration()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-health-{Guid.NewGuid()}")
            .Options;
        await using var database = new AppDbContext(options);
        using var redis = await ConnectionMultiplexer.ConnectAsync(
            "127.0.0.1:1,abortConnect=false,connectTimeout=50,syncTimeout=50");
        var controller = new HealthController(database, redis, IdentityConfiguration);

        var result = Assert.IsType<OkObjectResult>(await controller.GetAdminAIReady(default));
        var json = JsonSerializer.SerializeToElement(result.Value);

        Assert.Equal("healthy", json.GetProperty("status").GetString());
        Assert.Equal("disabled", json.GetProperty("feature").GetString());
        Assert.False(json.TryGetProperty("baselineHash", out _));
        Assert.False(json.TryGetProperty("callbackSecret", out _));
    }

    [Fact]
    public async Task AdminAIReady_WhenEnabled_NamesClosedReadinessStatesWithoutHashesOrSecrets()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-ai-health-enabled-{Guid.NewGuid()}")
            .Options;
        await using var database = new AppDbContext(options);
        database.AdminAICapabilityBaselines.Add(new AdminAICapabilityBaseline
        {
            Version = "sealed-1",
            ManifestHash = new string('a', 64),
            RuntimeInventoryHash = new string('b', 64),
            FrontendInventoryHash = new string('c', 64),
            SourceRevision = "test",
            Status = AdminAICapabilityBaselineStatus.Active
        });
        database.AdminAISensitiveDataPolicyVersions.Add(new AdminAISensitiveDataPolicyVersion
        {
            Version = "policy-1",
            PolicyHash = new string('d', 64),
            Status = AdminAISensitiveDataPolicyStatus.Active
        });
        await database.SaveChangesAsync();
        using var redis = await ConnectionMultiplexer.ConnectAsync(
            "127.0.0.1:1,abortConnect=false,connectTimeout=50,syncTimeout=50");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminAI:Enabled"] = "true",
            ["AdminAI:CallbackSecret"] = new string('s', 32)
        }).Build();
        var controller = new HealthController(database, redis, configuration);

        var result = Assert.IsType<ObjectResult>(await controller.GetAdminAIReady(default));
        var json = JsonSerializer.SerializeToElement(result.Value);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("enabled", json.GetProperty("feature").GetString());
        Assert.Equal("healthy", json.GetProperty("baseline").GetString());
        Assert.Equal("healthy", json.GetProperty("policy").GetString());
        Assert.Equal("unhealthy", json.GetProperty("queue").GetString());
        Assert.Equal("healthy", json.GetProperty("callbackAuthentication").GetString());
        Assert.False(json.TryGetProperty("baselineHash", out _));
        Assert.DoesNotContain(new string('s', 32), json.GetRawText(), StringComparison.Ordinal);
    }
}
