using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.API.BackgroundServices;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Background;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Cache;
using NaderGorge.Infrastructure.Services;
using StackExchange.Redis;

namespace NaderGorge.Integration.Tests;

public sealed class ClusterCoordinationTests : IAsyncLifetime
{
    private const string LeasePrefix = "integration-cluster-lease-";
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException(
            "PostgreSQL integration tests require ConnectionStrings__DefaultConnection.");

    public async Task InitializeAsync()
    {
        await using var database = CreateContext();
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cluster_leases (
                "Name" character varying(160) NOT NULL PRIMARY KEY,
                "OwnerToken" uuid NOT NULL,
                "FencingGeneration" bigint NOT NULL,
                "ExpiresAt" timestamp without time zone NOT NULL,
                "RenewedAt" timestamp without time zone NOT NULL,
                "LastOutcome" character varying(64)
            );
            """);
        await database.Database.ExecuteSqlRawAsync(
            """DELETE FROM cluster_leases WHERE "Name" LIKE 'integration-cluster-lease-%';""");
    }

    public async Task DisposeAsync()
    {
        await using var database = CreateContext();
        await database.Database.ExecuteSqlRawAsync(
            """DELETE FROM cluster_leases WHERE "Name" LIKE 'integration-cluster-lease-%';""");
    }

    [Fact]
    public async Task ConcurrentOwners_ProduceExactlyOneClaim()
    {
        var leaseName = $"{LeasePrefix}{Guid.NewGuid():N}";
        var owners = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        var durableEffects = 0;
        var claims = await Task.WhenAll(owners.Select(owner => AcquireAsync(
            leaseName,
            owner,
            TimeSpan.FromMinutes(1))));

        foreach (var claim in claims.Where(claim => claim is not null))
        {
            Interlocked.Increment(ref durableEffects);
        }

        Assert.Single(claims, claim => claim is not null);
        Assert.Equal(1, durableEffects);
        Assert.Equal(1, claims.Single(claim => claim is not null)!.FencingGeneration);
    }

    [Fact]
    public void SentinelConfiguration_UsesAllApprovedDiscoveryEndpoints()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Redis:Sentinels"] =
                    "10.77.0.11:26379,10.77.0.12:26379,10.77.0.13:26379",
                ["Redis:SentinelServiceName"] = "massar-redis",
                ["Redis:Password"] = "integration-only-password",
            })
            .Build();

        var options = RedisConnectionFactory.BuildConfiguration(configuration);

        Assert.Equal("massar-redis", options.ServiceName);
        Assert.Equal(3, options.EndPoints.Count);
        Assert.Equal(
            ["10.77.0.11:26379", "10.77.0.12:26379", "10.77.0.13:26379"],
            options.EndPoints.Select(endpoint => endpoint.ToString()!).ToArray());
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void ProductionRedis_RefusesAHiddenSingleNodeFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => RedisConnectionFactory.BuildConfiguration(configuration));

        Assert.Contains("Sentinel configuration is required", exception.Message);
    }

    [Fact]
    public async Task ExpiredOwner_IsFencedAndCannotRenewAfterTakeover()
    {
        var leaseName = $"{LeasePrefix}{Guid.NewGuid():N}";
        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        var firstClaim = await AcquireAsync(leaseName, firstOwner, TimeSpan.FromMilliseconds(50));
        Assert.NotNull(firstClaim);

        await Task.Delay(100);
        var secondClaim = await AcquireAsync(leaseName, secondOwner, TimeSpan.FromMinutes(1));
        Assert.NotNull(secondClaim);
        Assert.True(secondClaim!.FencingGeneration > firstClaim!.FencingGeneration);

        await using var staleContext = CreateContext();
        var staleService = new PostgresClusterLeaseService(staleContext);
        Assert.False(await staleService.RenewAsync(
            firstClaim,
            TimeSpan.FromMinutes(1),
            "stale",
            CancellationToken.None));
        Assert.True(await staleService.RenewAsync(
            secondClaim,
            TimeSpan.FromMinutes(1),
            "completed",
            CancellationToken.None));
    }

    [Fact]
    public async Task ClientOnFirstNode_ReceivesGroupMessagePublishedBySecondNode()
    {
        var redisConfiguration = CreateProductionRedisConfiguration();
        await using var firstNode = await SignalRTestNode.StartAsync(redisConfiguration);
        await using var secondNode = await SignalRTestNode.StartAsync(redisConfiguration);
        await using var client = new HubConnectionBuilder()
            .WithUrl($"{firstNode.Address}/cluster-coordination")
            .Build();
        var received = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = client.On<string>(
            "cluster-event",
            payload => received.TrySetResult(payload));
        var group = $"cluster-integration-{Guid.NewGuid():N}";
        var payload = Guid.NewGuid().ToString("N");

        await client.StartAsync();
        await client.InvokeAsync("JoinGroup", group);
        await secondNode.Hub.Clients.Group(group).SendAsync("cluster-event", payload);

        Assert.Equal(payload, await received.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task ReplayingSameOutboxEvent_ReusesExternalJobIdentity()
    {
        var redisConfiguration = CreateProductionRedisConfiguration();
        await using var redis = await ConnectionMultiplexer.ConnectAsync(redisConfiguration);
        var redisDatabase = redis.GetDatabase();
        var outboxEvent = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            Type = "CodeActivated",
            TargetUserId = Guid.NewGuid().ToString(),
            PayloadJson = """{"source":"cluster-integration"}""",
            CreatedAt = DateTime.UtcNow
        };
        await using var database = CreateContext();
        database.OutboxEvents.Add(outboxEvent);
        await database.SaveChangesAsync();
        var enqueuer = new RedisJobEnqueuer(redis);
        RedisValue[] createdStreamEntries = [];

        try
        {
            await ParentPurchaseOutboxDispatcher.DispatchAsync(outboxEvent, enqueuer);
            await ParentPurchaseOutboxDispatcher.DispatchAsync(outboxEvent, enqueuer);

            var matchingEntries = (await redisDatabase.StreamRangeAsync("job-stream"))
                .Where(entry => entry.Values.Any(field =>
                    field.Name == "jobId" &&
                    field.Value == outboxEvent.Id.ToString()))
                .ToArray();
            createdStreamEntries = matchingEntries.Select(entry => entry.Id).ToArray();

            Assert.Equal(2, matchingEntries.Length);
            Assert.Single(matchingEntries
                .Select(entry => entry.Values.Single(field => field.Name == "jobId").Value)
                .Distinct());
            Assert.All(matchingEntries, entry =>
                Assert.Equal(
                    "notification",
                    entry.Values.Single(field => field.Name == "jobType").Value));
        }
        finally
        {
            if (createdStreamEntries.Length == 0)
            {
                createdStreamEntries = (await redisDatabase.StreamRangeAsync("job-stream"))
                    .Where(entry => entry.Values.Any(field =>
                        field.Name == "jobId" &&
                        field.Value == outboxEvent.Id.ToString()))
                    .Select(entry => entry.Id)
                    .ToArray();
            }

            if (createdStreamEntries.Length > 0)
                await redisDatabase.StreamDeleteAsync("job-stream", createdStreamEntries);

            database.OutboxEvents.Remove(outboxEvent);
            await database.SaveChangesAsync();
        }
    }

    private async Task<NaderGorge.Application.Interfaces.ClusterLeaseClaim?> AcquireAsync(
        string name,
        Guid owner,
        TimeSpan lifetime)
    {
        await using var context = CreateContext();
        return await new PostgresClusterLeaseService(context).TryAcquireAsync(
            name,
            owner,
            lifetime,
            CancellationToken.None);
    }

    private static ConfigurationOptions CreateProductionRedisConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
            })
            .Build();

        return RedisConnectionFactory.BuildConfiguration(configuration);
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);
}

public sealed class ClusterCoordinationTestHub : Hub
{
    public Task JoinGroup(string group) =>
        Groups.AddToGroupAsync(Context.ConnectionId, group);
}

internal sealed class SignalRTestNode : IAsyncDisposable
{
    private readonly WebApplication _application;

    private SignalRTestNode(WebApplication application, string address)
    {
        _application = application;
        Address = address;
        Hub = application.Services
            .GetRequiredService<IHubContext<ClusterCoordinationTestHub>>();
    }

    public string Address { get; }
    public IHubContext<ClusterCoordinationTestHub> Hub { get; }

    public static async Task<SignalRTestNode> StartAsync(
        ConfigurationOptions redisConfiguration)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR()
            .AddStackExchangeRedis(options =>
            {
                options.Configuration = redisConfiguration.Clone();
                options.Configuration.ChannelPrefix =
                    RedisChannel.Literal("MassarSignalRIntegration");
            });
        var application = builder.Build();
        application.MapHub<ClusterCoordinationTestHub>("/cluster-coordination");
        await application.StartAsync();
        var address = application.Services
            .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses
            .Single()
            ?? throw new InvalidOperationException(
                "SignalR integration node did not publish a listening address.");

        return new SignalRTestNode(application, address);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}
