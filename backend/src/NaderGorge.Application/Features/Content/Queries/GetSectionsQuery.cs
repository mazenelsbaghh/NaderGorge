using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetSectionsQuery(Guid TermId, Guid? UserId = null) : IRequest<ApiResponse<List<ContentSectionDto>>>;

public record ContentSectionDto(Guid Id, string Title, int Order, decimal Price, string? ImageUrl, bool IsPurchased = false);

public class GetSectionsQueryHandler : IRequestHandler<GetSectionsQuery, ApiResponse<List<ContentSectionDto>>>
{
    private readonly IAppDbContext _db;

    public GetSectionsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<ContentSectionDto>>> Handle(GetSectionsQuery request, CancellationToken ct)
    {
        var sections = await _db.ContentSections
            .Where(cs => cs.TermId == request.TermId)
            .OrderBy(cs => cs.Order)
            .Select(cs => new { cs.Id, cs.Title, cs.Order, cs.Price, cs.ImageUrl })
            .ToListAsync(ct);

        // Determine which sections the student has already purchased (or has parent-level access)
        var purchasedSectionIds = new HashSet<Guid>();
        var hasParentAccess = false;

        if (request.UserId.HasValue)
        {
            var now = DateTime.UtcNow;

            // Get the term's parent package
            var term = await _db.Terms
                .FirstOrDefaultAsync(t => t.Id == request.TermId, ct);

            if (term != null)
            {
                // Check package-level access
                hasParentAccess = await _db.StudentAccessGrants
                    .AnyAsync(g => g.UserId == request.UserId.Value &&
                                   g.IsActive &&
                                   g.GrantType == CodeType.Package &&
                                   g.PackageId == term.PackageId &&
                                   (g.ExpiresAt == null || g.ExpiresAt > now), ct);

                if (!hasParentAccess)
                {
                    // Check term-level access
                    hasParentAccess = await _db.StudentAccessGrants
                        .AnyAsync(g => g.UserId == request.UserId.Value &&
                                       g.IsActive &&
                                       g.GrantType == CodeType.Term &&
                                       g.TermId == request.TermId &&
                                       (g.ExpiresAt == null || g.ExpiresAt > now), ct);
                }

                if (!hasParentAccess)
                {
                    // Check section-level grants
                    var sectionIds = sections.Select(s => s.Id).ToList();
                    purchasedSectionIds = (await _db.StudentAccessGrants
                        .Where(g => g.UserId == request.UserId.Value &&
                                    g.IsActive &&
                                    g.GrantType == CodeType.Month &&
                                    g.ContentSectionId != null &&
                                    sectionIds.Contains(g.ContentSectionId!.Value) &&
                                    (g.ExpiresAt == null || g.ExpiresAt > now))
                        .Select(g => g.ContentSectionId!.Value)
                        .ToListAsync(ct))
                        .ToHashSet();
                }
            }
        }

        var result = sections.Select(s => new ContentSectionDto(
            s.Id, s.Title, s.Order, s.Price, s.ImageUrl,
            hasParentAccess || purchasedSectionIds.Contains(s.Id)
        )).ToList();

        return ApiResponse<List<ContentSectionDto>>.Ok(result);
    }
}
