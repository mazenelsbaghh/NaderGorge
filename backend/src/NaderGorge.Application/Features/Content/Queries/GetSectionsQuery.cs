using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetSectionsQuery(Guid TermId) : IRequest<ApiResponse<List<ContentSectionDto>>>;

public record ContentSectionDto(Guid Id, string Title, int Order, decimal Price);

public class GetSectionsQueryHandler : IRequestHandler<GetSectionsQuery, ApiResponse<List<ContentSectionDto>>>
{
    private readonly IAppDbContext _db;

    public GetSectionsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<ContentSectionDto>>> Handle(GetSectionsQuery request, CancellationToken ct)
    {
        var sections = await _db.ContentSections
            .Where(cs => cs.TermId == request.TermId)
            .OrderBy(cs => cs.Order)
            .Select(cs => new ContentSectionDto(cs.Id, cs.Title, cs.Order, cs.Price))
            .ToListAsync(ct);

        return ApiResponse<List<ContentSectionDto>>.Ok(sections);
    }
}
