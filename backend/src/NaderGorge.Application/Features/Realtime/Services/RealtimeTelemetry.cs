using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NaderGorge.Application.Features.Realtime.Services;

public readonly record struct OutboxTelemetryDimensions(
    string EventType,
    string NodeId,
    string ReleaseId);

public static class RealtimeTelemetry
{
    private static readonly HashSet<string> AllowedEventTypes =
        new(StringComparer.Ordinal)
        {
            "BalanceChanged",
            "CodeActivated",
            "EssayEvaluationQueued",
            "ExtraWatchRequestUpdated",
            "LiveSupportAITurnQueued",
            "LiveSupportEvent",
            "NotificationCreated",
            "PackageAccessGranted",
            "PurchaseCompleted",
            "StaffDataChanged"
        };

    public const string MeterName = "NaderGorge.Realtime";
    public const string ClaimedName = "outbox.events.claimed";
    public const string ClaimWaitName = "outbox.claim.wait";
    public const string DispatchedName = "outbox.events.dispatched";
    public const string DispatchFailuresName = "outbox.dispatch.failures";
    public const string RetriesName = "outbox.events.retried";
    public const string DeadLettersName = "outbox.events.dead_letters";
    public const string DispatchDurationName = "outbox.dispatch.duration";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Claimed =
        Meter.CreateCounter<long>(ClaimedName);
    private static readonly Histogram<double> ClaimWait =
        Meter.CreateHistogram<double>(ClaimWaitName, "ms");
    private static readonly Counter<long> Dispatched =
        Meter.CreateCounter<long>(DispatchedName);
    private static readonly Counter<long> DispatchFailures =
        Meter.CreateCounter<long>(DispatchFailuresName);
    private static readonly Counter<long> Retries =
        Meter.CreateCounter<long>(RetriesName);
    private static readonly Counter<long> DeadLetters =
        Meter.CreateCounter<long>(DeadLettersName);
    private static readonly Histogram<double> DispatchDuration =
        Meter.CreateHistogram<double>(DispatchDurationName, "ms");

    public static OutboxTelemetryDimensions Dimensions(
        string eventType,
        string nodeId,
        string releaseId) =>
        new(
            AllowedEventTypes.Contains(eventType) ? eventType : "other",
            SafeDimension(nodeId),
            SafeDimension(releaseId));

    public static void RecordClaim(
        OutboxTelemetryDimensions dimensions,
        double waitMilliseconds)
    {
        var tags = Tags(dimensions);
        Claimed.Add(1, tags);
        ClaimWait.Record(Math.Max(0, waitMilliseconds), tags);
    }

    public static void RecordDispatchSucceeded(
        OutboxTelemetryDimensions dimensions,
        double durationMilliseconds)
    {
        var tags = DispatchTags(dimensions, "success");
        Dispatched.Add(1, tags);
        DispatchDuration.Record(durationMilliseconds, tags);
    }

    public static void RecordDispatchFailed(
        OutboxTelemetryDimensions dimensions,
        double durationMilliseconds)
    {
        var tags = DispatchTags(dimensions, "failure");
        DispatchFailures.Add(1, tags);
        DispatchDuration.Record(durationMilliseconds, tags);
    }

    public static void RecordRetry(OutboxTelemetryDimensions dimensions) =>
        Retries.Add(1, Tags(dimensions));

    public static void RecordDeadLetter(OutboxTelemetryDimensions dimensions) =>
        DeadLetters.Add(1, Tags(dimensions));

    private static TagList Tags(OutboxTelemetryDimensions dimensions)
    {
        TagList tags = default;
        tags.Add("event_type", dimensions.EventType);
        tags.Add("node", dimensions.NodeId);
        tags.Add("release", dimensions.ReleaseId);
        return tags;
    }

    private static TagList DispatchTags(
        OutboxTelemetryDimensions dimensions,
        string outcome)
    {
        var tags = Tags(dimensions);
        tags.Add("outcome", outcome);
        return tags;
    }

    private static string SafeDimension(string dimension) =>
        dimension is { Length: > 0 and <= 64 } &&
        dimension.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.')
            ? dimension
            : "other";
}
