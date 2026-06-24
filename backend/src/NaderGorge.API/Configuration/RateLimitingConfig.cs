using System.Threading.RateLimiting;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;

namespace NaderGorge.API.Configuration;

public static class RateLimitingConfig
{
    public static IServiceCollection AddRateLimitingPolicies(this IServiceCollection services)
    {
        var isE2e = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "E2e";

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new { code = "RATE_LIMITED", retryAfterSeconds = 60 }, cancellationToken);
            };

            // Auth endpoints: 30 requests per minute per IP
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Code activation: 20 requests per minute per user
            options.AddPolicy("codes", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Video Sessions: 30 requests per minute per user (prevent brute forcing encrypt token gen)
            options.AddPolicy("video-session", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // AI Analysis: 5 requests per minute per user
            options.AddPolicy("ai-analysis", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Sign Download: 10 requests per minute per user
            options.AddPolicy("sign-download", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // General API: 1000 requests per minute per IP
            options.AddPolicy("public-whatsapp", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 12,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("public-forms", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("parent-report", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("live-support-public", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("live-support-action", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            AddLiveSupportPolicy(options, "live-support-ai-message", isE2e ? 100000 : 10, ParticipantKey);
            AddLiveSupportPolicy(options, "live-support-ai-confirmation", isE2e ? 100000 : 20, ParticipantKey);
            AddLiveSupportPolicy(options, "live-support-ai-verification", isE2e ? 100000 : 10, ParticipantKey);
            AddLiveSupportPolicy(options, "live-support-ai-registration", isE2e ? 100000 : 5, ParticipantKey);
            AddLiveSupportPolicy(options, "live-support-ai-admin-preview", isE2e ? 100000 : 10, context => context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            AddLiveSupportPolicy(options, "live-support-ai-callback", isE2e ? 100000 : 120, context => context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 1000,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static void AddLiveSupportPolicy(RateLimiterOptions options, string name, int limit, Func<HttpContext, string> partition) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(partition(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    private static string ParticipantKey(HttpContext context)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId)) return $"user:{userId}";
        if (context.Request.Cookies.TryGetValue("massar_support_guest", out var guest) && !string.IsNullOrEmpty(guest))
            return $"guest:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(guest)))[..24]}";
        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
