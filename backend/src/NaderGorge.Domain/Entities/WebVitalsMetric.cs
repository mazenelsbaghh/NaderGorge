using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class WebVitalsMetric : BaseEntity
{
    public string MetricId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty; // LCP, CLS, INP, FID, FCP, TTFB
    public double Value { get; set; }
    public string Rating { get; set; } = string.Empty; // good, needs-improvement, poor
    public string RouteTemplate { get; set; } = string.Empty;
    public string Surface { get; set; } = string.Empty;
    public string DeviceClass { get; set; } = string.Empty;
    public string ConnectionClass { get; set; } = string.Empty;
    public string NavigationType { get; set; } = string.Empty;
    public string ReleaseId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }

    // Legacy columns remain during the compatibility window. New ingest never
    // accepts or stores their high-cardinality values.
    public string PageUrl { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}
