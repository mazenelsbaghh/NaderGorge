using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NaderGorge.Infrastructure.Observability;

public readonly record struct RequestPerformanceMeasurement(
    string Route,
    string Method,
    int StatusCode,
    double DurationMilliseconds,
    string NodeId,
    string ReleaseId,
    long DbCommandCount,
    double DbCommandDurationMilliseconds);

public static class RequestPerformanceMetrics
{
    public const string MeterName = "NaderGorge.Requests";
    public const string DurationName = "http.server.request.duration";
    public const string DbCommandCountName = "http.server.request.db_commands";
    public const string DbCommandDurationName = "http.server.request.db_duration";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>(DurationName, "ms");
    private static readonly Histogram<long> DbCommandCount =
        Meter.CreateHistogram<long>(DbCommandCountName, "{command}");
    private static readonly Histogram<double> DbCommandDuration =
        Meter.CreateHistogram<double>(DbCommandDurationName, "ms");

    public static void Record(RequestPerformanceMeasurement measurement)
    {
        var tags = Tags(measurement);
        RequestDuration.Record(measurement.DurationMilliseconds, tags);
        DbCommandCount.Record(measurement.DbCommandCount, tags);
        DbCommandDuration.Record(measurement.DbCommandDurationMilliseconds, tags);
    }

    private static TagList Tags(RequestPerformanceMeasurement measurement)
    {
        TagList tags = default;
        tags.Add("route", measurement.Route);
        tags.Add("method", measurement.Method);
        tags.Add("status_code", measurement.StatusCode);
        tags.Add("outcome", measurement.StatusCode < 500 ? "success" : "failure");
        tags.Add("node", measurement.NodeId);
        tags.Add("release", measurement.ReleaseId);
        return tags;
    }
}
