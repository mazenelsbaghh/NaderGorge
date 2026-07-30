using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Metrics.Commands;

public record CreateWebVitalsMetricCommand(
    string MetricName,
    double Value,
    string Rating,
    string PageUrl,
    string UserAgent
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
        var metric = new WebVitalsMetric
        {
            MetricName = request.MetricName,
            Value = request.Value,
            Rating = request.Rating,
            PageUrl = request.PageUrl,
            UserAgent = request.UserAgent
        };

        _db.WebVitalsMetrics.Add(metric);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(metric.Id);
    }
}
