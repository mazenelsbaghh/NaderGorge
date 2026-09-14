using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using NaderGorge.Application.Features.Realtime.Services;

namespace NaderGorge.Integration.Tests.Observability;

public sealed class OutboxTelemetryTests
{
    [Fact]
    public void OutboxLifecycle_RecordsBoundedDimensionsWithoutPayloadContent()
    {
        var measurements = new ConcurrentQueue<MetricMeasurement>();
        using var listener = CreateListener(measurements);
        var dimensions = RealtimeTelemetry.Dimensions(
            "StaffDataChanged",
            "node-3",
            "src-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        RealtimeTelemetry.RecordClaim(dimensions, 14);
        RealtimeTelemetry.RecordDispatchSucceeded(dimensions, 8);
        RealtimeTelemetry.RecordDispatchFailed(dimensions, 12);
        RealtimeTelemetry.RecordRetry(dimensions);
        RealtimeTelemetry.RecordDeadLetter(dimensions);

        Assert.Equal(8, measurements.Count);
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName == RealtimeTelemetry.ClaimWaitName &&
                measurement.Measurement == 14);
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName ==
                    RealtimeTelemetry.DispatchDurationName &&
                Equals(measurement.Tags["outcome"], "success"));
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName ==
                    RealtimeTelemetry.DispatchDurationName &&
                Equals(measurement.Tags["outcome"], "failure"));
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName == RealtimeTelemetry.RetriesName);
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName == RealtimeTelemetry.DeadLettersName);
        Assert.All(measurements, AssertSafeDimensions);
    }

    [Theory]
    [InlineData("StaffDataChanged", "StaffDataChanged")]
    [InlineData("NovelSyntacticallySafeEvent", "other")]
    [InlineData("event-with-user-123456", "other")]
    public void EventTypeDimension_UsesFiniteTaxonomy(
        string eventType,
        string expectedDimension)
    {
        var dimensions = RealtimeTelemetry.Dimensions(
            eventType,
            "node-1",
            "release-1");

        Assert.Equal(expectedDimension, dimensions.EventType);
    }

    private static MeterListener CreateListener(
        ConcurrentQueue<MetricMeasurement> measurements)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (instrument.Meter.Name == RealtimeTelemetry.MeterName)
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

    private static void AssertSafeDimensions(MetricMeasurement measurement)
    {
        var allowedTags = new HashSet<string>
        {
            "event_type",
            "node",
            "outcome",
            "release",
        };
        Assert.True(
            measurement.Tags.Keys.ToHashSet(StringComparer.Ordinal)
                .IsSubsetOf(allowedTags));
        Assert.Equal("StaffDataChanged", measurement.Tags["event_type"]);
        Assert.Equal("node-3", measurement.Tags["node"]);
        Assert.DoesNotContain("payload", measurement.Tags.Keys);
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
}
