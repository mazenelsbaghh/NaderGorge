using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetTermsQuery(Guid PackageId, Guid? UserId = null) : IRequest<ApiResponse<List<TermDto>>>;

public class GetTermsQueryHandler : IRequestHandler<GetTermsQuery, ApiResponse<List<TermDto>>>
{
    private readonly IAppDbContext _db;
    public GetTermsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<TermDto>>> Handle(GetTermsQuery request, CancellationToken ct)
    {
        var terms = await _db.Terms
            .Where(t => t.PackageId == request.PackageId)
            .OrderBy(t => t.Order)
            .Select(t => new { t.Id, t.Title, t.Order, t.Price, t.ImageUrl })
            .ToListAsync(ct);

        // Determine which terms the student has already purchased
        var purchasedTermIds = new HashSet<Guid>();
        var hasPackageAccess = false;

        if (request.UserId.HasValue)
        {
            var now = DateTime.UtcNow;

            // Check if user has package-level access (covers all terms)
            hasPackageAccess = await _db.StudentAccessGrants
                .AnyAsync(g => g.UserId == request.UserId.Value &&
                               g.IsActive &&
                               g.GrantType == CodeType.Package &&
                               g.PackageId == request.PackageId &&
                               (g.ExpiresAt == null || g.ExpiresAt > now), ct);

            if (!hasPackageAccess)
            {
                // Check term-level grants
                var termIds = terms.Select(t => t.Id).ToList();
                purchasedTermIds = (await _db.StudentAccessGrants
                    .Where(g => g.UserId == request.UserId.Value &&
                                g.IsActive &&
                                g.GrantType == CodeType.Term &&
                                g.TermId != null &&
                                termIds.Contains(g.TermId!.Value) &&
                                (g.ExpiresAt == null || g.ExpiresAt > now))
                    .Select(g => g.TermId!.Value)
                    .ToListAsync(ct))
                    .ToHashSet();
            }
        }

        var result = terms.Select(t => new TermDto(
            t.Id, t.Title, t.Order, t.Price, t.ImageUrl,
            hasPackageAccess || purchasedTermIds.Contains(t.Id)
        )).ToList();

        return ApiResponse<List<TermDto>>.Ok(result);
    }
}
