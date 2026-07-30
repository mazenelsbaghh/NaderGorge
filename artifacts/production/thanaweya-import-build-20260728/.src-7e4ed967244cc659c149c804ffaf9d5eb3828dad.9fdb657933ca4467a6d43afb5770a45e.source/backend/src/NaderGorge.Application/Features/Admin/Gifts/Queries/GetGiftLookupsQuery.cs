using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Gifts.Queries;

public sealed record GetGiftStudentsLookupQuery(string? Search = null) : IRequest<ApiResponse<IReadOnlyList<GiftLookupDto>>>;
public sealed record GetGiftTeachersLookupQuery(string? Search = null) : IRequest<ApiResponse<IReadOnlyList<GiftLookupDto>>>;
public sealed record GetGiftTargetsLookupQuery(GiftTargetType TargetType, Guid? TeacherId = null, string? Search = null) : IRequest<ApiResponse<IReadOnlyList<GiftLookupDto>>>;

public sealed class GetGiftStudentsLookupQueryHandler : IRequestHandler<GetGiftStudentsLookupQuery, ApiResponse<IReadOnlyList<GiftLookupDto>>>
{
    private readonly IAppDbContext _db;
    public GetGiftStudentsLookupQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<IReadOnlyList<GiftLookupDto>>> Handle(GetGiftStudentsLookupQuery request, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking().Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Type == RoleType.Student));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(search) || x.PhoneNumber.Contains(search));
        }
        var rows = await query.OrderBy(x => x.FullName).Take(50)
            .Select(x => new GiftLookupDto(x.Id, x.FullName, x.PhoneNumber, null)).ToListAsync(ct);
        return ApiResponse<IReadOnlyList<GiftLookupDto>>.Ok(rows);
    }
}

public sealed class GetGiftTeachersLookupQueryHandler : IRequestHandler<GetGiftTeachersLookupQuery, ApiResponse<IReadOnlyList<GiftLookupDto>>>
{
    private readonly IAppDbContext _db;
    public GetGiftTeachersLookupQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<IReadOnlyList<GiftLookupDto>>> Handle(GetGiftTeachersLookupQuery request, CancellationToken ct)
    {
        var query = _db.TeacherProfiles.AsNoTracking().Where(x => x.User.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.User.FullName.ToLower().Contains(search));
        }
        var rows = await query.OrderBy(x => x.User.FullName).Take(50)
            .Select(x => new GiftLookupDto(x.Id, x.User.FullName, x.Specialization, null)).ToListAsync(ct);
        return ApiResponse<IReadOnlyList<GiftLookupDto>>.Ok(rows);
    }
}

public sealed class GetGiftTargetsLookupQueryHandler : IRequestHandler<GetGiftTargetsLookupQuery, ApiResponse<IReadOnlyList<GiftLookupDto>>>
{
    private readonly IAppDbContext _db;
    public GetGiftTargetsLookupQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<IReadOnlyList<GiftLookupDto>>> Handle(GetGiftTargetsLookupQuery request, CancellationToken ct)
    {
        var search = request.Search?.Trim().ToLower();
        List<GiftLookupDto> rows = request.TargetType switch
        {
            GiftTargetType.Package => await _db.Packages.AsNoTracking()
                .Where(x => x.IsActive && (!request.TeacherId.HasValue || x.TeacherId == request.TeacherId) && (search == null || x.Name.ToLower().Contains(search)))
                .OrderBy(x => x.Name).Take(50).Select(x => new GiftLookupDto(x.Id, x.Name, x.Teacher.User.FullName, null)).ToListAsync(ct),
            GiftTargetType.Lesson => await _db.Lessons.AsNoTracking()
                .Where(x => (!request.TeacherId.HasValue || x.ContentSection.Term.Package.TeacherId == request.TeacherId) && (search == null || x.Title.ToLower().Contains(search) || x.InternalCode.ToLower().Contains(search)))
                .OrderBy(x => x.Title).Take(50).Select(x => new GiftLookupDto(x.Id, x.Title, x.ContentSection.Term.Package.Name, null)).ToListAsync(ct),
            GiftTargetType.Video => await _db.LessonVideos.AsNoTracking()
                .Where(x => x.IsActive && (!request.TeacherId.HasValue || x.Lesson.ContentSection.Term.Package.TeacherId == request.TeacherId) && (search == null || x.Title.ToLower().Contains(search) || x.InternalCode.ToLower().Contains(search)))
                .OrderBy(x => x.Title).Take(50).Select(x => new GiftLookupDto(x.Id, x.Title, x.Lesson.Title, null)).ToListAsync(ct),
            GiftTargetType.Exam => await _db.Exams.AsNoTracking()
                .Where(x => (!request.TeacherId.HasValue || x.CreatedByTeacherId == request.TeacherId) && (search == null || x.Title.ToLower().Contains(search) || x.InternalCode.ToLower().Contains(search)))
                .OrderBy(x => x.Title).Take(50).Select(x => new GiftLookupDto(x.Id, x.Title, x.CreatedByTeacher.User.FullName, null)).ToListAsync(ct),
            _ => new List<GiftLookupDto>()
        };

        var rowsWithScopes = new List<GiftLookupDto>(rows.Count);
        foreach (var row in rows)
        {
            rowsWithScopes.Add(row with { AcademicScopes = await ResolveScopeSummariesAsync(_db, request.TargetType, row.Id, ct) });
        }

        return ApiResponse<IReadOnlyList<GiftLookupDto>>.Ok(rowsWithScopes);
    }

    private static async Task<IReadOnlyList<AcademicScopeSummaryDto>?> ResolveScopeSummariesAsync(
        IAppDbContext db,
        GiftTargetType targetType,
        Guid targetId,
        CancellationToken ct)
    {
        var ownerType = targetType switch
        {
            GiftTargetType.Package => StudentFacingScopeOwnerType.Package,
            GiftTargetType.Lesson => StudentFacingScopeOwnerType.Lesson,
            GiftTargetType.Video => StudentFacingScopeOwnerType.LessonVideo,
            GiftTargetType.Exam => StudentFacingScopeOwnerType.Exam,
            _ => (StudentFacingScopeOwnerType?)null
        };

        if (!ownerType.HasValue)
            return null;

        var scopes = await db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == ownerType.Value && x.OwnerId == targetId)
            .ToListAsync(ct);

        return AcademicScopeService.ToScopeSummaries(scopes);
    }
}
