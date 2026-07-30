using System.Diagnostics.Metrics;

namespace NaderGorge.Application.Features.LiveSupport.Services;

public static class LiveSupportTelemetry
{
    private static readonly Meter Meter = new("NaderGorge.LiveSupport", "1.0.0");
    public static readonly Counter<long> ConversationsCreated = Meter.CreateCounter<long>("live_support.conversations.created");
    public static readonly Counter<long> AssignmentsReleased = Meter.CreateCounter<long>("live_support.assignments.released");
    public static readonly Counter<long> ActionsSucceeded = Meter.CreateCounter<long>("live_support.actions.succeeded");
    public static readonly Counter<long> ActionsFailed = Meter.CreateCounter<long>("live_support.actions.failed");
    public static readonly Histogram<double> AttachmentBytes = Meter.CreateHistogram<double>("live_support.attachments.bytes", "bytes");
}
