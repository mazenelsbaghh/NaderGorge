using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Admin.Queries;

public record AdminFormDto(
    Guid Id,
    string Title,
    string Description,
    string Slug,
    bool IsActive,
    string? CoverImageUrl,
    int VisitCount,
    int SubmissionCount,
    DateTime CreatedAt
);

public record AdminFormDetailDto(
    Guid Id,
    string Title,
    string Description,
    string Slug,
    bool IsActive,
    string? CoverImageUrl,
    string FieldsJson,
    DateTime CreatedAt
);

public record AdminSubmissionDto(
    Guid Id,
    Guid CustomFormId,
    string SubmittedDataJson,
    FormSubmissionStatus Status,
    string? AdminNotes,
    DateTime SubmittedAt
);

// --- Queries ---
public record ListFormsQuery : IRequest<ApiResponse<List<AdminFormDto>>>;

public class ListFormsQueryHandler : IRequestHandler<ListFormsQuery, ApiResponse<List<AdminFormDto>>>
{
    private readonly IAppDbContext _db;
    public ListFormsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<AdminFormDto>>> Handle(ListFormsQuery request, CancellationToken ct)
    {
        var forms = await _db.CustomForms
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new AdminFormDto(
                f.Id,
                f.Title,
                f.Description,
                f.Slug,
                f.IsActive,
                f.CoverImageUrl,
                f.VisitCount,
                f.Submissions.Count,
                f.CreatedAt
            ))
            .ToListAsync(ct);

        return ApiResponse<List<AdminFormDto>>.Ok(forms);
    }
}

public record GetFormDetailsQuery(Guid Id) : IRequest<ApiResponse<AdminFormDetailDto>>;

public class GetFormDetailsQueryHandler : IRequestHandler<GetFormDetailsQuery, ApiResponse<AdminFormDetailDto>>
{
    private readonly IAppDbContext _db;
    public GetFormDetailsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<AdminFormDetailDto>> Handle(GetFormDetailsQuery request, CancellationToken ct)
    {
        var form = await _db.CustomForms
            .Where(f => f.Id == request.Id)
            .Select(f => new AdminFormDetailDto(
                f.Id,
                f.Title,
                f.Description,
                f.Slug,
                f.IsActive,
                f.CoverImageUrl,
                f.FieldsJson,
                f.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);

        if (form == null) return ApiResponse<AdminFormDetailDto>.Fail("النموذج غير موجود.");

        return ApiResponse<AdminFormDetailDto>.Ok(form);
    }
}

public record ListSubmissionsQuery(Guid FormId) : IRequest<ApiResponse<List<AdminSubmissionDto>>>;

public class ListSubmissionsQueryHandler : IRequestHandler<ListSubmissionsQuery, ApiResponse<List<AdminSubmissionDto>>>
{
    private readonly IAppDbContext _db;
    public ListSubmissionsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<AdminSubmissionDto>>> Handle(ListSubmissionsQuery request, CancellationToken ct)
    {
        var submissions = await _db.FormSubmissions
            .Where(s => s.CustomFormId == request.FormId)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new AdminSubmissionDto(
                s.Id,
                s.CustomFormId,
                s.SubmittedDataJson,
                s.Status,
                s.AdminNotes,
                s.SubmittedAt
            ))
            .ToListAsync(ct);

        return ApiResponse<List<AdminSubmissionDto>>.Ok(submissions);
    }
}
