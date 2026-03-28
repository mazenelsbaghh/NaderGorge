using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetTermByIdQuery(Guid Id) : IRequest<ApiResponse<TermDetailDto>>;

public record TermDetailDto(Guid Id, string Title, int Order, Guid PackageId, decimal Price);

public class GetTermByIdQueryHandler : IRequestHandler<GetTermByIdQuery, ApiResponse<TermDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetTermByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<TermDetailDto>> Handle(GetTermByIdQuery request, CancellationToken ct)
    {
        var term = await _db.Terms
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        if (term == null)
            return ApiResponse<TermDetailDto>.Fail("Term not found");

        var dto = new TermDetailDto(term.Id, term.Title, term.Order, term.PackageId, term.Price);
        
        return ApiResponse<TermDetailDto>.Ok(dto);
    }
}
