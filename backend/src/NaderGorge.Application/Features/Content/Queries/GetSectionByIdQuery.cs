using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetSectionByIdQuery(Guid Id) : IRequest<ApiResponse<SectionDetailDto>>;

public record SectionDetailDto(Guid Id, string Title, int Order, Guid TermId, Guid PackageId, decimal Price, string? ImageUrl, bool IsDirect, PackageContentMode ContentMode, ContentArchiveMode ArchiveMode, DateTime? ArchivedAt);

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
            .Include(s => s.Term)
                .ThenInclude(term => term.Package)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);

        if (section == null)
            return ApiResponse<SectionDetailDto>.Fail("Section not found");

        var dto = new SectionDetailDto(
            section.Id,
            section.Title,
            section.Order,
            section.TermId,
            section.Term.PackageId,
            section.Price,
            section.ImageUrl,
            section.Term.IsSystemContainer,
            section.Term.Package.ContentMode,
            section.ArchiveMode,
            section.ArchivedAt);

        return ApiResponse<SectionDetailDto>.Ok(dto);
    }
}
