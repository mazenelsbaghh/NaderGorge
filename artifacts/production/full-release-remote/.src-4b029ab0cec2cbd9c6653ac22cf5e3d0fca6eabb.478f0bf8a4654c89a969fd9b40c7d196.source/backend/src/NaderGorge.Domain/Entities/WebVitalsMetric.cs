using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class WebVitalsMetric : BaseEntity
{
    public string MetricName { get; set; } = string.Empty; // LCP, CLS, INP, FID, FCP, TTFB
    public double Value { get; set; }
    public string Rating { get; set; } = string.Empty; // good, needs-improvement, poor
    public string PageUrl { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}
