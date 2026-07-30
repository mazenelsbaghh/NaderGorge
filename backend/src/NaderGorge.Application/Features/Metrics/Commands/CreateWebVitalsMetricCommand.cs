using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Metrics.Commands;

public record CreateWebVitalsMetricCommand(
    string MetricId,
    string MetricName,
    double Value,
    string Rating,
    string RouteTemplate,
    string Surface,
    string DeviceClass,
    string ConnectionClass,
    string NavigationType,
    string ReleaseId,
    string? CorrelationId = null
) : IRequest<ApiResponse<Guid>>;

public class CreateWebVitalsMetricCommandHandler : IRequestHandler<CreateWebVitalsMetricCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateWebVitalsMetricCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateWebVitalsMetricCommand request, CancellationToken ct)
    {
        if (!WebVitalsContract.TryNormalize(request, out var normalized))
        {
            return ApiResponse<Guid>.Fail("Invalid Web Vitals metric.");
        }

        var metric = new WebVitalsMetric
        {
            MetricId = normalized.MetricId,
            MetricName = normalized.MetricName,
            Value = request.Value,
            Rating = normalized.Rating,
            RouteTemplate = normalized.RouteTemplate,
            Surface = normalized.Surface,
            DeviceClass = normalized.DeviceClass,
            ConnectionClass = normalized.ConnectionClass,
            NavigationType = normalized.NavigationType,
            ReleaseId = normalized.ReleaseId,
            CorrelationId = normalized.CorrelationId,
            PageUrl = string.Empty,
            UserAgent = string.Empty
        };

        _db.WebVitalsMetrics.Add(metric);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(metric.Id);
    }
}

public static class WebVitalsContract
{
    private static readonly HashSet<string> MetricNames = new(StringComparer.Ordinal)
        { "LCP", "CLS", "INP", "FID", "FCP", "TTFB" };
    private static readonly HashSet<string> Ratings = new(StringComparer.Ordinal)
        { "good", "needs-improvement", "poor" };
    private static readonly HashSet<string> Surfaces = new(StringComparer.Ordinal)
        { "public", "student", "parent", "teacher", "assistant", "employee", "admin", "support", "unknown" };
    private static readonly HashSet<string> DeviceClasses = new(StringComparer.Ordinal)
        { "mobile", "tablet", "desktop", "unknown" };
    private static readonly HashSet<string> ConnectionClasses = new(StringComparer.Ordinal)
        { "fast", "moderate", "slow", "offline", "unknown" };
    private static readonly HashSet<string> NavigationTypes = new(StringComparer.Ordinal)
        { "navigate", "client", "reload", "back-forward", "prerender", "unknown" };
    private static readonly HashSet<string> DynamicParentSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "assistants", "codes", "conversations", "coupons", "exams", "forms",
        "gifts", "groups", "homework", "lessons", "packages", "sections",
        "students", "teachers", "terms", "users", "videos"
    };

    public static bool TryNormalize(
        CreateWebVitalsMetricCommand request,
        out CreateWebVitalsMetricCommand normalized)
    {
        normalized = request;
        if (!double.IsFinite(request.Value) || request.Value < 0 ||
            !MetricNames.Contains(request.MetricName) ||
            !Ratings.Contains(request.Rating) ||
            !Surfaces.Contains(request.Surface) ||
            !DeviceClasses.Contains(request.DeviceClass) ||
            !ConnectionClasses.Contains(request.ConnectionClass) ||
            !NavigationTypes.Contains(request.NavigationType) ||
            !Bounded(request.MetricId, 64) ||
            !Bounded(request.ReleaseId, 96) ||
            (request.CorrelationId is not null && !Bounded(request.CorrelationId, 64)))
        {
            return false;
        }

        var route = NormalizeRouteTemplate(request.RouteTemplate);
        if (route is null)
        {
            return false;
        }

        normalized = request with
        {
            MetricId = request.MetricId.Trim(),
            RouteTemplate = route,
            ReleaseId = request.ReleaseId.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? null
                : request.CorrelationId.Trim()
        };
        return true;
    }

    public static string? NormalizeRouteTemplate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var path = value.Split('?', '#')[0].Trim();
        if (!path.StartsWith('/') || path.Length > 180 ||
            path.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (Guid.TryParse(segment, out _) ||
                (index > 0 && DynamicParentSegments.Contains(segments[index - 1])))
            {
                segments[index] = RouteParameterName(segments, index);
            }
            else if (segment.Length > 0 && segment.All(char.IsDigit))
            {
                segments[index] = "[id]";
            }
            else if (segment.Length > 64 ||
                segment.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '[' or ']')))
            {
                return null;
            }
        }

        return segments.Length == 0 ? "/" : "/" + string.Join('/', segments);
    }

    private static string RouteParameterName(IReadOnlyList<string> segments, int index) =>
        index > 0 && string.Equals(segments[index - 1], "packages", StringComparison.OrdinalIgnoreCase)
            ? "[packageId]"
            : "[id]";

    private static bool Bounded(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;
}
