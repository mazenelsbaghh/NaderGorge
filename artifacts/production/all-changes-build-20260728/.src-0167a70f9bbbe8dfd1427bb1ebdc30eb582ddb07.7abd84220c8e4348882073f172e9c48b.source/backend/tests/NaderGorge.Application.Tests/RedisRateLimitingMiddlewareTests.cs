using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.API.Middleware;
using StackExchange.Redis;

namespace NaderGorge.Application.Tests;

public sealed class RedisIntegrationTheoryAttribute : TheoryAttribute
{
    public RedisIntegrationTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_REDIS_INTEGRATION_TESTS") != "1")
        {
            Skip = "Set RUN_REDIS_INTEGRATION_TESTS=1 to run Redis integration tests.";
        }
    }
}

public class RedisRateLimitingMiddlewareTests
{
    public static TheoryData<string, int, bool> Policies => new()
    {
        { "auth", 30, false },
        { "codes", 20, true },
        { "video-session", 30, true },
        { "ai-analysis", 5, true },
        { "sign-download", 10, true },
        { "public-whatsapp", 12, false },
        { "public-forms", 20, false },
        { "parent-report", 30, false }
    };

    [RedisIntegrationTheory]
    [MemberData(nameof(Policies))]
    public async Task Policy_ExceedingPermitLimit_Returns429(
        string policyName,
        int permitLimit,
        bool limitByUser)
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_REDIS_CONNECTION")
            ?? "localhost:6379,abortConnect=false";
        await using var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
        var partition = limitByUser ? Guid.NewGuid().ToString("N") : "127.0.0.1";
        var redisKey = $"ratelimit:{policyName}:{partition}";
        await redis.GetDatabase().KeyDeleteAsync(redisKey);

        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            var nextCalls = 0;
            var middleware = new RedisRateLimitingMiddleware(
                _ =>
                {
                    nextCalls++;
                    return Task.CompletedTask;
                },
                redis);

            for (var requestNumber = 1; requestNumber <= permitLimit + 1; requestNumber++)
            {
                var context = CreateContext(policyName, partition, limitByUser);
                await middleware.InvokeAsync(context);

                var expectedStatus = requestNumber <= permitLimit
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status429TooManyRequests;
                Assert.Equal(expectedStatus, context.Response.StatusCode);
            }

            Assert.Equal(permitLimit, nextCalls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
            await redis.GetDatabase().KeyDeleteAsync(redisKey);
        }
    }

    private static DefaultHttpContext CreateContext(
        string policyName,
        string partition,
        bool limitByUser)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(policyName)),
            $"Rate limit test: {policyName}"));

        if (limitByUser)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, partition)],
                "TestAuth"));
        }
        else
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        }

        return context;
    }
}
