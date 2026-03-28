using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetSectionByIdQuery(Guid Id) : IRequest<ApiResponse<SectionDetailDto>>;

public record SectionDetailDto(Guid Id, string Title, int Order, Guid TermId, decimal Price);

public class GetSectionByIdQueryHandler : IRequestHandler<GetSectionByIdQuery, ApiResponse<SectionDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetSectionByIdQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<SectionDetailDto>> Handle(GetSectionByIdQuery request, CancellationToken ct)
    {
        var section = await _db.ContentSections
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);

        if (section == null)
            return ApiResponse<SectionDetailDto>.Fail("Section not found");

        var dto = new SectionDetailDto(section.Id, section.Title, section.Order, section.TermId, section.Price);
        
        return ApiResponse<SectionDetailDto>.Ok(dto);
    }
}
