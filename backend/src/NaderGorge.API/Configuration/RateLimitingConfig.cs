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

            // Auth endpoints: 10 requests per minute per IP
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Code activation: 5 requests per minute per user
            options.AddPolicy("codes", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Video Sessions: 10 requests per minute per user (prevent brute forcing encrypt token gen)
            options.AddPolicy("video-session", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // General API: 300 requests per minute per IP (increased for admin dashboard polling)
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isE2e ? 100000 : 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }
}
