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
    string? CoverImageUrl,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
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
            .Where(f => f.Slug == lowerSlug)
            .FirstOrDefaultAsync(ct);

        if (form == null || !form.IsActive) 
            return ApiResponse<PublicFormDto>.Fail("النموذج المطلوب غير موجود أو غير مفعل حالياً.");

        var now = DateTime.UtcNow;
        if (form.StartsAt.HasValue && form.StartsAt.Value > now)
        {
            var localTime = form.StartsAt.Value.AddHours(2);
            return ApiResponse<PublicFormDto>.Fail($"عذراً، هذا النموذج لم يبدأ بعد. سيبدأ استقبال الطلبات في {localTime:yyyy-MM-dd HH:mm}.");
        }

        if (form.ExpiresAt.HasValue && form.ExpiresAt.Value < now)
        {
            return ApiResponse<PublicFormDto>.Fail("عذراً، لقد انتهت صلاحية هذا النموذج وتم إغلاق استقبال الطلبات به.");
        }

        form.VisitCount += 1;
        form.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<PublicFormDto>.Ok(new PublicFormDto(
            form.Id,
            form.Title,
            form.Description,
            form.CoverImageUrl,
            form.StartsAt,
            form.ExpiresAt,
            form.FieldsJson
        ));
    }
}
