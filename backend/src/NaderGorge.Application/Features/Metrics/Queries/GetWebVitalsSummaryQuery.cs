using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Metrics.Queries;

public sealed record GetWebVitalsSummaryQuery(
    string? ReleaseId,
    string? RouteTemplate,
    string? Surface,
    string? DeviceClass,
    DateTime? From,
    DateTime? To
) : IRequest<ApiResponse<WebVitalsSummaryDto>>;

public sealed record WebVitalsSummaryFiltersDto(
    string? ReleaseId,
    string? RouteTemplate,
    string? Surface,
    string? DeviceClass,
    DateTime From,
    DateTime To);

public sealed record WebVitalsSegmentDto(
    string MetricName,
    double P50,
    double P75,
    double P90,
    double P99,
    double GoodRate);

public sealed record WebVitalsSummaryDto(
    WebVitalsSummaryFiltersDto Filters,
    int SampleCount,
    IReadOnlyList<WebVitalsSegmentDto> Segments,
    bool SampleQualified);

public sealed class GetWebVitalsSummaryQueryHandler(
    IAppDbContext db
) : IRequestHandler<GetWebVitalsSummaryQuery, ApiResponse<WebVitalsSummaryDto>>
{
    private const int MaximumSummarySamples = 50_000;
    private static readonly HashSet<string> Surfaces = new(StringComparer.Ordinal)
        { "public", "student", "parent", "teacher", "assistant", "employee", "admin", "support", "unknown" };
    private static readonly HashSet<string> DeviceClasses = new(StringComparer.Ordinal)
        { "mobile", "tablet", "desktop", "unknown" };

    public async Task<ApiResponse<WebVitalsSummaryDto>> Handle(
        GetWebVitalsSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var to = request.To?.ToUniversalTime() ?? DateTime.UtcNow;
        var from = request.From?.ToUniversalTime() ?? to.AddHours(-24);
        if (from >= to || to - from > TimeSpan.FromDays(31))
        {
            return ApiResponse<WebVitalsSummaryDto>.Fail("Invalid metrics summary window.");
        }
        if (request.ReleaseId is { Length: > 96 } ||
            (request.Surface is not null && !Surfaces.Contains(request.Surface.Trim())) ||
            (request.DeviceClass is not null && !DeviceClasses.Contains(request.DeviceClass.Trim())))
        {
            return ApiResponse<WebVitalsSummaryDto>.Fail("Invalid metrics summary filter.");
        }

        var route = request.RouteTemplate is null
            ? null
            : Commands.WebVitalsContract.NormalizeRouteTemplate(request.RouteTemplate);
        if (request.RouteTemplate is not null && route is null)
        {
            return ApiResponse<WebVitalsSummaryDto>.Fail("Invalid route template.");
        }

        var query = db.WebVitalsMetrics
            .AsNoTracking()
            .Where(metric => metric.CreatedAt >= from && metric.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(request.ReleaseId))
            query = query.Where(metric => metric.ReleaseId == request.ReleaseId.Trim());
        if (route is not null)
            query = query.Where(metric => metric.RouteTemplate == route);
        if (!string.IsNullOrWhiteSpace(request.Surface))
            query = query.Where(metric => metric.Surface == request.Surface.Trim());
        if (!string.IsNullOrWhiteSpace(request.DeviceClass))
            query = query.Where(metric => metric.DeviceClass == request.DeviceClass.Trim());

        var samples = await query
            .OrderByDescending(metric => metric.CreatedAt)
            .Select(metric => new { metric.MetricName, metric.Value, metric.Rating })
            .Take(MaximumSummarySamples)
            .ToListAsync(cancellationToken);

        var segments = samples
            .GroupBy(sample => sample.MetricName, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var values = group.Select(sample => sample.Value).Order().ToArray();
                return new WebVitalsSegmentDto(
                    group.Key,
                    Percentile(values, 0.50),
                    Percentile(values, 0.75),
                    Percentile(values, 0.90),
                    Percentile(values, 0.99),
                    group.Count(sample => sample.Rating == "good") / (double)group.Count());
            })
            .ToArray();

        return ApiResponse<WebVitalsSummaryDto>.Ok(new(
            new(
                request.ReleaseId?.Trim(),
                route,
                request.Surface?.Trim(),
                request.DeviceClass?.Trim(),
                from,
                to),
            samples.Count,
            segments,
            samples.Count >= 100));
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        var rank = percentile * (sorted.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (rank - lower));
    }
}
