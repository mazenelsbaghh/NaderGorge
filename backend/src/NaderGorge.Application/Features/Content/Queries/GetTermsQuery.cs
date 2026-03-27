using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetTermsQuery(Guid PackageId) : IRequest<ApiResponse<List<TermDto>>>;

public record TermDto(Guid Id, string Title, int Order);

public class GetTermsQueryHandler : IRequestHandler<GetTermsQuery, ApiResponse<List<TermDto>>>
{
    private readonly IAppDbContext _db;
    public GetTermsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<TermDto>>> Handle(GetTermsQuery request, CancellationToken ct)
    {
        var terms = await _db.Terms
            .Where(t => t.PackageId == request.PackageId)
            .OrderBy(t => t.Order)
            .Select(t => new TermDto(t.Id, t.Title, t.Order))
            .ToListAsync(ct);

        return ApiResponse<List<TermDto>>.Ok(terms);
    }
}
