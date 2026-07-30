using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using NaderGorge.Infrastructure.Observability;

namespace NaderGorge.API.Middleware;

public class RequestPerformanceLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestPerformanceLoggingMiddleware> _logger;
    private readonly string _nodeId;
    private readonly string _releaseId;
    private const int ThresholdMs = 500;

    public RequestPerformanceLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestPerformanceLoggingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _nodeId = SafeIdentity(configuration["Cluster:NodeId"]);
        _releaseId = SafeIdentity(configuration["Cluster:ReleaseId"]);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var databaseCommands = RequestDbCommandScope.Begin();
        try
        {
            await _next(context);
        }
        finally
        {
            RecordEvidence(context, databaseCommands, startedAt);
        }
    }

    private void RecordEvidence(
        HttpContext context,
        RequestDbCommandScope databaseCommands,
        long startedAt)
    {
        if (context.Response.StatusCode == StatusCodes.Status101SwitchingProtocols)
        {
            return;
        }

        var measurement = Measurement(context, databaseCommands, startedAt);
        RequestPerformanceMetrics.Record(measurement);

        if (measurement.DurationMilliseconds <= ThresholdMs)
        {
            return;
        }

        _logger.LogWarning(
            "Slow request. CorrelationId={CorrelationId} Route={Route} Method={Method} Status={Status} DurationMs={DurationMs} Node={Node} Release={Release} EfCommandCount={EfCommandCount} EfCommandDurationMs={EfCommandDurationMs}",
            context.Items[CorrelationIdMiddleware.CorrelationIdItem],
            measurement.Route,
            measurement.Method,
            measurement.StatusCode,
            measurement.DurationMilliseconds,
            measurement.NodeId,
            measurement.ReleaseId,
            measurement.DbCommandCount,
            measurement.DbCommandDurationMilliseconds);
    }

    private RequestPerformanceMeasurement Measurement(
        HttpContext context,
        RequestDbCommandScope databaseCommands,
        long startedAt) =>
        new(
            NormalizedRoute(context),
            NormalizedMethod(context.Request.Method),
            context.Response.StatusCode,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            _nodeId,
            _releaseId,
            databaseCommands.CommandCount,
            databaseCommands.CommandDurationMilliseconds);

    private static string NormalizedRoute(HttpContext context)
    {
        var route =
            (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        return route is { Length: > 0 and <= 160 } ? route : "unmatched";
    }

    private static string NormalizedMethod(string method) =>
        method.ToUpperInvariant() switch
        {
            "GET" => "GET",
            "POST" => "POST",
            "PUT" => "PUT",
            "PATCH" => "PATCH",
            "DELETE" => "DELETE",
            "HEAD" => "HEAD",
            "OPTIONS" => "OPTIONS",
            _ => "OTHER"
        };

    private static string SafeIdentity(string? identity) =>
        identity is { Length: > 0 and <= 64 } &&
        identity.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.')
            ? identity
            : "unknown";
}
