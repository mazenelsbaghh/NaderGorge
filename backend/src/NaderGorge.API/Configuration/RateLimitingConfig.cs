using System.Threading.RateLimiting;

namespace NaderGorge.API.Configuration;

public static class RateLimitingConfig
{
    public static IServiceCollection AddRateLimitingPolicies(this IServiceCollection services)
    {
        var isE2e = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "E2e";

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;

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
}
