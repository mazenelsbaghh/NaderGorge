using System.Diagnostics.Metrics;

namespace NaderGorge.Application.Features.Realtime.Services;

public static class RealtimeTelemetry
{
    private static readonly Meter Meter = new("NaderGorge.Realtime", "1.0.0");

    public static readonly Counter<long> EventsDispatched =
        Meter.CreateCounter<long>("realtime.events.dispatched");
    public static readonly Counter<long> DispatchFailures =
        Meter.CreateCounter<long>("realtime.events.dispatch_failures");
    public static readonly Counter<long> DeadLetters =
        Meter.CreateCounter<long>("realtime.events.dead_letters");
    public static readonly Histogram<double> DispatchLatency =
        Meter.CreateHistogram<double>("realtime.events.dispatch_latency", "ms");
}
