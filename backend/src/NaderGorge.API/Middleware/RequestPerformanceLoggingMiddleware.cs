using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NaderGorge.API.Middleware;

public class RequestPerformanceLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestPerformanceLoggingMiddleware> _logger;
    private const int ThresholdMs = 500; // 500ms performance budget threshold

    public RequestPerformanceLoggingMiddleware(RequestDelegate next, ILogger<RequestPerformanceLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            if (elapsedMs > ThresholdMs)
            {
                var method = context.Request.Method;
                var path = context.Request.Path;
                var queryString = context.Request.QueryString.ToString();
                _logger.LogWarning("Slow Request Detected: {Method} {Path}{Query} took {ElapsedMs}ms (Threshold: {Threshold}ms)",
                    method, path, queryString, elapsedMs, ThresholdMs);
            }
        }
    }
}
