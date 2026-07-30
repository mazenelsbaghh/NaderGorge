using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NaderGorge.API.Middleware;
using NaderGorge.Infrastructure.Observability;

namespace NaderGorge.Integration.Tests.Observability;

public sealed class PerformanceCorrelationTests
{
    [Fact]
    public async Task SafeCorrelationId_PropagatesThroughResponseAndStructuredEvidence()
    {
        using var capture = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        using var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .BuildServiceProvider();
        var context = CreateContext(services);
        context.Request.Headers["X-Correlation-Id"] = "corr-observe-123";

        var performance = new RequestPerformanceLoggingMiddleware(
            async downstreamContext =>
            {
                await Task.Delay(510);
                downstreamContext.Response.StatusCode = StatusCodes.Status200OK;
            },
            loggerFactory.CreateLogger<RequestPerformanceLoggingMiddleware>(),
            PerformanceConfiguration());
        var correlation = new CorrelationIdMiddleware(performance.InvokeAsync);

        await correlation.InvokeAsync(context);

        Assert.Equal("corr-observe-123", context.Response.Headers["X-Correlation-Id"]);
        Assert.Contains(
            capture.Evidence,
            entry => entry.Contains("corr-observe-123", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RequestEvidence_ExcludesHeadersQueryBodyAndUnsafeCorrelationValues()
    {
        const string secret = "PRIVACY_SENTINEL_SUPPORT_CONTENT";
        using var capture = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        using var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .BuildServiceProvider();
        var context = CreateContext(services);
        context.Request.Headers.Authorization = $"Bearer {secret}";
        context.Request.Headers.Cookie = $"session={secret}";
        context.Request.Headers["X-Correlation-Id"] = secret;
        context.Request.QueryString = new QueryString($"?access_token={secret}");
        context.Request.Body = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes($"{{\"message\":\"{secret}\"}}"));

        var performance = new RequestPerformanceLoggingMiddleware(
            async downstreamContext =>
            {
                await Task.Delay(510);
                downstreamContext.Response.StatusCode = StatusCodes.Status200OK;
            },
            loggerFactory.CreateLogger<RequestPerformanceLoggingMiddleware>(),
            PerformanceConfiguration());
        var correlation = new CorrelationIdMiddleware(performance.InvokeAsync);

        await correlation.InvokeAsync(context);

        var responseCorrelation = context.Response.Headers["X-Correlation-Id"].ToString();
        Assert.DoesNotContain(secret, responseCorrelation, StringComparison.Ordinal);
        Assert.InRange(responseCorrelation.Length, 16, 64);
        Assert.All(
            capture.Evidence,
            entry => Assert.DoesNotContain(secret, entry, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RequestEvidence_UsesRouteTemplateAndRequestScopedEfTotals()
    {
        var measurements = new ConcurrentQueue<MetricMeasurement>();
        using var listener = CreateMetricsListener(measurements);
        using var loggerFactory = LoggerFactory.Create(builder => { });
        using var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .BuildServiceProvider();
        var context = CreateContext(services);
        context.Request.Path =
            "/api/users/PRIVACY_SENTINEL_USER_ID";
        context.Request.QueryString =
            new QueryString("?access_token=PRIVACY_SENTINEL_TOKEN");
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/users/{userId:guid}"),
            0,
            EndpointMetadataCollection.Empty,
            "user-detail"));

        var performance = new RequestPerformanceLoggingMiddleware(
            downstreamContext =>
            {
                DbCommandMetricsInterceptor.RecordCommand(
                    DbCommandMethod.ExecuteReader,
                    TimeSpan.FromMilliseconds(8),
                    DbCommandOutcome.Success);
                DbCommandMetricsInterceptor.RecordCommand(
                    DbCommandMethod.ExecuteScalar,
                    TimeSpan.FromMilliseconds(3),
                    DbCommandOutcome.Success);
                downstreamContext.Response.StatusCode =
                    StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            loggerFactory.CreateLogger<RequestPerformanceLoggingMiddleware>(),
            PerformanceConfiguration());

        await performance.InvokeAsync(context);

        var requestMeasurements = measurements
            .Where(measurement =>
                Equals(
                    measurement.Tags["release"],
                    "src-0123456789012345678901234567890123456789"))
            .ToArray();
        Assert.Equal(3, requestMeasurements.Length);
        Assert.All(requestMeasurements, AssertSafeRequestTags);
        Assert.Contains(
            requestMeasurements,
            measurement =>
                measurement.InstrumentName ==
                    RequestPerformanceMetrics.DbCommandCountName &&
                measurement.Measurement == 2);
        Assert.Contains(
            requestMeasurements,
            measurement =>
                measurement.InstrumentName ==
                    RequestPerformanceMetrics.DbCommandDurationName &&
                measurement.Measurement == 11);
    }

    [Fact]
    public async Task WebSocket101Response_IsExcludedFromHttpLatencyEvidence()
    {
        const string nodeId = "node-websocket-test";
        var measurements = new ConcurrentQueue<MetricMeasurement>();
        using var listener = CreateMetricsListener(measurements);
        using var capture = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(capture));
        using var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .BuildServiceProvider();
        var context = CreateContext(services);
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/hubs/live-support";
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/hubs/live-support"),
            0,
            EndpointMetadataCollection.Empty,
            "live-support-hub"));

        var performance = new RequestPerformanceLoggingMiddleware(
            async downstreamContext =>
            {
                await Task.Delay(510);
                downstreamContext.Response.StatusCode =
                    StatusCodes.Status101SwitchingProtocols;
            },
            loggerFactory.CreateLogger<RequestPerformanceLoggingMiddleware>(),
            PerformanceConfiguration(nodeId));

        await performance.InvokeAsync(context);

        Assert.DoesNotContain(
            capture.Evidence,
            entry => entry.Contains("Slow request", StringComparison.Ordinal));
        Assert.DoesNotContain(
            measurements,
            measurement =>
                measurement.Tags.TryGetValue("node", out var node) &&
                Equals(node, nodeId));
    }

    [Fact]
    public async Task ErrorAwarePerformancePipeline_RecordsHandledExceptionAsFailure()
    {
        const string nodeId = "node-exception-test";
        var measurements = new ConcurrentQueue<MetricMeasurement>();
        using var listener = CreateMetricsListener(measurements);
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(PerformanceConfiguration(nodeId))
            .BuildServiceProvider();
        var context = CreateContext(services);
        var pipeline = new ApplicationBuilder(services);
        pipeline.UseErrorAwareRequestPerformance();
        pipeline.Run(_ => throw new Exception("production incident"));

        await pipeline.Build()(context);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);
        var requestMeasurements = measurements
            .Where(measurement =>
                measurement.Tags.TryGetValue("node", out var node) &&
                Equals(node, nodeId))
            .ToArray();
        Assert.Equal(3, requestMeasurements.Length);
        Assert.All(
            requestMeasurements,
            measurement =>
            {
                Assert.Equal(
                    StatusCodes.Status500InternalServerError,
                    measurement.Tags["status_code"]);
                Assert.Equal("failure", measurement.Tags["outcome"]);
            });
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services,
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/live-support/participant/conversations/[conversationId]/messages";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static IConfiguration PerformanceConfiguration(
        string nodeId = "node-test") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cluster:NodeId"] = nodeId,
                ["Cluster:ReleaseId"] = "src-0123456789012345678901234567890123456789",
            })
            .Build();

    private static MeterListener CreateMetricsListener(
        ConcurrentQueue<MetricMeasurement> measurements)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (instrument.Meter.Name == RequestPerformanceMetrics.MeterName)
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) =>
                measurements.Enqueue(Capture(instrument, measurement, tags)));
        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, _) =>
                measurements.Enqueue(Capture(instrument, measurement, tags)));
        listener.Start();
        return listener;
    }

    private static MetricMeasurement Capture(
        Instrument instrument,
        double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        new(
            instrument.Name,
            measurement,
            tags.ToArray().ToDictionary(entry => entry.Key, entry => entry.Value));

    private static void AssertSafeRequestTags(MetricMeasurement measurement)
    {
        Assert.Equal(
            ["method", "node", "outcome", "release", "route", "status_code"],
            measurement.Tags.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("/api/users/{userId:guid}", measurement.Tags["route"]);
        Assert.Equal("POST", measurement.Tags["method"]);
        Assert.Equal(204, measurement.Tags["status_code"]);
        Assert.Equal("success", measurement.Tags["outcome"]);
        Assert.Equal("node-test", measurement.Tags["node"]);
        Assert.DoesNotContain(
            measurement.Tags.Values,
            tag => tag?.ToString()?.Contains(
                "PRIVACY_SENTINEL",
                StringComparison.Ordinal) == true);
    }

    private sealed record MetricMeasurement(
        string InstrumentName,
        double Measurement,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class CapturingLoggerProvider :
        ILoggerProvider,
        ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public List<string> Evidence { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
            => _scopes = scopeProvider;

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => owner._scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var evidence = new List<string> { formatter(state, exception) };
                owner._scopes.ForEachScope(
                    (scope, entries) => entries.Add(RenderScope(scope)),
                    evidence);
                owner.Evidence.Add(string.Join(" | ", evidence));
            }

            private static string RenderScope(object? scope)
            {
                if (scope is IEnumerable<KeyValuePair<string, object>> fields)
                {
                    return string.Join(
                        ",",
                        fields.Select(field => $"{field.Key}={field.Value}"));
                }

                return scope?.ToString() ?? string.Empty;
            }
        }
    }
}
