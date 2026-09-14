using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Infrastructure.Observability;

namespace NaderGorge.Integration.Tests.Performance;

public sealed class DbCommandMetricsInterceptorTests
{
    [Fact]
    public void CompletedCommands_RecordOnlyBoundedSafeMetrics()
    {
        var measurements = new ConcurrentQueue<MetricMeasurement>();
        using var listener = CreateListener(measurements);

        DbCommandMetricsInterceptor.RecordCommand(
            DbCommandMethod.ExecuteReader,
            TimeSpan.FromMilliseconds(12),
            DbCommandOutcome.Success);
        DbCommandMetricsInterceptor.RecordCommand(
            DbCommandMethod.ExecuteNonQuery,
            TimeSpan.FromMilliseconds(5),
            DbCommandOutcome.Failure);

        AssertMeasurementsAreSafe(measurements);
    }

    private static MeterListener CreateListener(
        ConcurrentQueue<MetricMeasurement> measurements)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (instrument.Meter.Name == DbCommandMetricsInterceptor.MeterName)
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
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var capturedTags = new Dictionary<string, object?>();
        foreach (var tag in tags)
        {
            capturedTags.Add(tag.Key, tag.Value);
        }

        return new MetricMeasurement(instrument.Name, measurement, capturedTags);
    }

    private static void AssertMeasurementsAreSafe(
        ConcurrentQueue<MetricMeasurement> measurements)
    {
        Assert.True(measurements.Count >= 4);
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName == DbCommandMetricsInterceptor.CommandCountName &&
                measurement.Tags["success"] is true);
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName == DbCommandMetricsInterceptor.CommandCountName &&
                measurement.Tags["success"] is false);
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName == DbCommandMetricsInterceptor.CommandDurationName &&
                measurement.Measurement == 12 &&
                Equals(measurement.Tags["operation"], "reader") &&
                measurement.Tags["success"] is true);
        Assert.Contains(
            measurements,
            measurement =>
                measurement.InstrumentName == DbCommandMetricsInterceptor.CommandDurationName &&
                measurement.Measurement == 5 &&
                Equals(measurement.Tags["operation"], "non_query") &&
                measurement.Tags["success"] is false);

        Assert.All(measurements, measurement =>
        {
            Assert.Equal(
                ["operation", "success"],
                measurement.Tags.Keys.Order(StringComparer.Ordinal));
            Assert.Contains(
                measurement.Tags["operation"],
                new object?[] { "reader", "non_query", "scalar" });
            Assert.IsType<bool>(measurement.Tags["success"]);
            Assert.True(measurement.Measurement >= 0);
        });
    }

    private sealed record MetricMeasurement(
        string InstrumentName,
        double Measurement,
        IReadOnlyDictionary<string, object?> Tags);
}
