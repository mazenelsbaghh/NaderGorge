using System.Diagnostics.Metrics;

namespace NaderGorge.Application.Features.LiveSupportAI.Services;

public static class LiveSupportAITelemetry
{
    private static readonly Meter Meter = new("NaderGorge.LiveSupportAI", "1.0.0");
    public static readonly Counter<long> TurnsQueued = Meter.CreateCounter<long>("live_support_ai.turns.queued");
    public static readonly Counter<long> CallbackOutcomes = Meter.CreateCounter<long>("live_support_ai.callbacks");
    public static readonly Counter<long> Handoffs = Meter.CreateCounter<long>("live_support_ai.handoffs");
    public static readonly Counter<long> RecoveryOutcomes = Meter.CreateCounter<long>("live_support_ai.recovery");
    public static readonly Histogram<double> InferenceLatency = Meter.CreateHistogram<double>("live_support_ai.inference.latency", "ms");
}
