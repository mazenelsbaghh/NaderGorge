using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Public.Queries;

public record PublicFormDto(
    Guid Id,
    string Title,
    string Description,
    string FieldsJson
);

public record GetPublicFormQuery(string Slug) : IRequest<ApiResponse<PublicFormDto>>;

public class GetPublicFormQueryHandler : IRequestHandler<GetPublicFormQuery, ApiResponse<PublicFormDto>>
{
    private readonly IAppDbContext _db;
    public GetPublicFormQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<PublicFormDto>> Handle(GetPublicFormQuery request, CancellationToken ct)
    {
        var lowerSlug = request.Slug.ToLowerInvariant();
        var form = await _db.CustomForms
            .Where(f => f.Slug == lowerSlug && f.IsActive)
            .FirstOrDefaultAsync(ct);

        if (form == null) return ApiResponse<PublicFormDto>.Fail("النموذج المطلوب غير موجود أو غير مفعل حالياً.");

        form.VisitCount += 1;
        form.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<PublicFormDto>.Ok(new PublicFormDto(
            form.Id,
            form.Title,
            form.Description,
            form.FieldsJson
        ));
    }
}
