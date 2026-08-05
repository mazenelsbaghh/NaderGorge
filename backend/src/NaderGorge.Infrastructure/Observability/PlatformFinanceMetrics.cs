using System.Diagnostics.Metrics;

namespace NaderGorge.Infrastructure.Observability;

public sealed class PlatformFinanceMetrics : IDisposable
{
    private readonly Meter _meter = new("NaderGorge.PlatformFinance", "1.0");
    private readonly Counter<long> _postings;
    private readonly Counter<long> _duplicates;
    private readonly Histogram<double> _postingLatency;
    private readonly Histogram<double> _queryLatency;

    public PlatformFinanceMetrics()
    {
        _postings = _meter.CreateCounter<long>("finance.postings", description: "Financial journal postings.");
        _duplicates = _meter.CreateCounter<long>("finance.duplicate_retries", description: "Idempotent duplicate posting retries.");
        _postingLatency = _meter.CreateHistogram<double>("finance.posting_latency_ms", unit: "ms");
        _queryLatency = _meter.CreateHistogram<double>("finance.query_latency_ms", unit: "ms");
    }

    public void RecordPosting(double milliseconds, bool duplicate = false)
    {
        _postings.Add(1);
        _postingLatency.Record(milliseconds);
        if (duplicate) _duplicates.Add(1);
    }

    public void RecordQuery(double milliseconds, string queryName) => _queryLatency.Record(milliseconds, new KeyValuePair<string, object?>("query", queryName));

    public void Dispose() => _meter.Dispose();
}
