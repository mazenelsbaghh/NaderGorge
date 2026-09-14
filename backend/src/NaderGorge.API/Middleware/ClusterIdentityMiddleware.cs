namespace NaderGorge.API.Middleware;

public sealed class ClusterIdentityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _nodeId;
    private readonly string _releaseId;

    public ClusterIdentityMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _nodeId = configuration["Cluster:NodeId"] ?? "unknown";
        _releaseId = configuration["Cluster:ReleaseId"] ?? "unknown";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Massar-Node"] = _nodeId;
            context.Response.Headers["X-Massar-Release"] = _releaseId;
            return Task.CompletedTask;
        });
        await _next(context);
    }
}
