using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace NaderGorge.API.Middleware;

public class RedisRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _db;
    private readonly bool _isE2e;

    private static readonly Dictionary<string, (int PermitLimit, TimeSpan Window, bool LimitByUser)> Policies = new()
    {
        { "auth", (30, TimeSpan.FromMinutes(1), false) },
        { "codes", (20, TimeSpan.FromMinutes(1), true) },
        { "video-session", (30, TimeSpan.FromMinutes(1), true) },
        { "ai-analysis", (5, TimeSpan.FromMinutes(1), true) },
        { "sign-download", (10, TimeSpan.FromMinutes(1), true) },
        { "public-whatsapp", (12, TimeSpan.FromMinutes(1), false) },
        { "public-forms", (20, TimeSpan.FromMinutes(1), false) },
        { "parent-report", (30, TimeSpan.FromMinutes(1), false) },
        { "live-support-public", (20, TimeSpan.FromMinutes(1), false) },
        { "live-support-action", (30, TimeSpan.FromMinutes(1), true) },
        { "live-support-ai-message", (10, TimeSpan.FromMinutes(1), true) },
        { "live-support-ai-confirmation", (20, TimeSpan.FromMinutes(1), true) },
        { "live-support-ai-verification", (10, TimeSpan.FromMinutes(1), true) },
        { "live-support-ai-registration", (5, TimeSpan.FromMinutes(1), true) },
        { "live-support-ai-admin-preview", (10, TimeSpan.FromMinutes(1), true) },
        { "live-support-ai-callback", (120, TimeSpan.FromMinutes(1), false) }
    };

    public RedisRateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next = next;
        _db = redis.GetDatabase();
        _isE2e = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "E2e";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        var disableAttr = endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>();
        if (disableAttr != null)
        {
            await _next(context);
            return;
        }

        var enableAttr = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        string? policyName = enableAttr?.PolicyName;

        int permitLimit;
        TimeSpan window;
        bool limitByUser;

        if (!string.IsNullOrEmpty(policyName) && Policies.TryGetValue(policyName, out var config))
        {
            permitLimit = _isE2e ? 100000 : config.PermitLimit;
            window = config.Window;
            limitByUser = config.LimitByUser;
        }
        else
        {
            policyName = "global";
            permitLimit = _isE2e ? 100000 : 1000;
            window = TimeSpan.FromMinutes(1);
            limitByUser = false;
        }

        string partitionKey;
        if (limitByUser)
        {
            partitionKey = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? GuestOrIp(context);
        }
        else
        {
            partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        var redisKey = $"ratelimit:{policyName}:{partitionKey}";

        var luaScript = @"
            local current = redis.call('INCR', KEYS[1])
            if current == 1 then
                redis.call('EXPIRE', KEYS[1], ARGV[1])
            end
            return current";

        var result = await _db.ScriptEvaluateAsync(
            luaScript, 
            new RedisKey[] { redisKey }, 
            new RedisValue[] { (int)window.TotalSeconds });

        var requestCount = (long)result;

        if (requestCount > permitLimit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers.RetryAfter = Math.Max(1, (int)window.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            
            var response = new { Code = "RATE_LIMITED", Message = "طلبات كثيرة في وقت قصير. انتظر لحظات ثم حاول مرة أخرى.", RetryAfterSeconds = (int)window.TotalSeconds };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            return;
        }

        await _next(context);
    }

    private static string GuestOrIp(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue("massar_support_guest", out var token) && !string.IsNullOrEmpty(token))
            return $"guest:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))[..24]}";
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
